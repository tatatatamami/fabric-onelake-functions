using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace function_onelake.Endpoints;

public class GetEmployeesBySql
{
    private readonly ILogger<GetEmployeesBySql> _logger;
    private readonly TokenCredential _credential;

    public GetEmployeesBySql(ILogger<GetEmployeesBySql> logger, TokenCredential credential)
    {
        _logger = logger;
        _credential = credential;
    }

    [Function("GetEmployeesBySql")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees/sql")] HttpRequestData req)
    {
        _logger.LogInformation("Processing SQL employees aggregation request.");

        // 1) ���ϐ�
        var sqlEndpoint = Environment.GetEnvironmentVariable("SQL_ENDPOINT");
        var sqlDatabase = Environment.GetEnvironmentVariable("SQL_DATABASE");

        if (string.IsNullOrWhiteSpace(sqlEndpoint) || string.IsNullOrWhiteSpace(sqlDatabase))
        {
            var respBad = req.CreateResponse(HttpStatusCode.InternalServerError);
            await respBad.WriteAsJsonAsync(new
            {
                error = "Database configuration missing. Please set SQL_ENDPOINT and SQL_DATABASE environment variables."
            });
            return respBad;
        }

        // 2) �N�G���擾 (?department=IT �Ȃ�)
        string? department = null;
        var q = QueryHelpers.ParseQuery(req.Url.Query);
        if (q.TryGetValue("department", out var depVals))
        {
            department = depVals.ToString();
        }

        try
        {
            // 3) Entra ID �g�[�N���擾
            var scope = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
            var token = await _credential.GetTokenAsync(scope, default);
            _logger.LogInformation("Access token acquired successfully.");

            // 4) �ڑ�������쐬
            var csb = new SqlConnectionStringBuilder
            {
                DataSource = sqlEndpoint,     // ��: "<xxx>.datawarehouse.fabric.microsoft.com"
                InitialCatalog = sqlDatabase, // ��: "fabricdemo"
                Encrypt = true,
                TrustServerCertificate = false,
                ConnectTimeout = 30
            };

            using var conn = new SqlConnection(csb.ConnectionString)
            {
                AccessToken = token.Token
            };
            await conn.OpenAsync();

            // 5) SQL (�W�v�� DB ���փv�b�V���_�E��)
            string sql;
            var cmd = conn.CreateCommand();

            if (!string.IsNullOrWhiteSpace(department))
            {
                sql = @"
                    SELECT 
                      COUNT(*) AS Total,
                      AVG(CAST(salary AS FLOAT)) AS AverageSalary
                    FROM dbo.sample_employees
                    WHERE department = @department;";
                cmd.Parameters.Add(new SqlParameter("@department", department));
            }
            else
            {
                sql = @"
                    SELECT 
                      COUNT(*) AS Total,
                      AVG(CAST(salary AS FLOAT)) AS AverageSalary
                    FROM dbo.sample_employees;";
            }

            cmd.CommandText = sql;

            using var reader = await cmd.ExecuteReaderAsync();
            int total = 0;
            int averageSalary = 0;

            if (await reader.ReadAsync())
            {
                total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                if (!reader.IsDBNull(1))
                {
                    var avg = reader.GetDouble(1);
                    averageSalary = (int)Math.Round(avg);
                }
            }

            // 6) �����쐬�inull ���폜�j
            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var payload = new
            {
                total,
                department = string.IsNullOrWhiteSpace(department) ? null : department,
                averageSalary
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            await resp.WriteStringAsync(json);
            return resp;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error occurred while querying employee data.");
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteAsJsonAsync(new
            {
                error = "Database connection failed. Please check the SQL endpoint configuration and ensure the database is accessible."
            });
            return resp;
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogError(ex, "Authentication failed while connecting to database.");
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteAsJsonAsync(new
            {
                error = "Authentication failed. Please ensure Entra ID authentication is properly configured."
            });
            return resp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while processing SQL employees request.");
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteAsJsonAsync(new
            {
                error = "An unexpected error occurred while processing the request."
            });
            return resp;
        }
    }
}
