using System.Globalization;
using System.Net;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using function_onelake.Models;

namespace function_onelake.Endpoints;

// PoC implementation: reads a CSV file from OneLake and filters rows in memory.
//
// Limitations:
//   - Every request downloads and scans the entire CSV file (full table scan).
//   - Memory usage and latency grow linearly with the size of the CSV file.
//   - Not suitable for large datasets or production workloads.
//
// For production use, consider GET /api/employees/sql which pushes aggregation
// to the Fabric SQL endpoint (Lakehouse / Warehouse) for scalable, engine-side processing.
public class GetEmployeesFiltered
{
    private readonly ILogger<GetEmployeesFiltered> _logger;
    private const int MaxItems = 50;

    public GetEmployeesFiltered(ILogger<GetEmployeesFiltered> logger)
    {
        _logger = logger;
    }

    [Function("GetEmployeesFiltered")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Processing GET /api/employees request (PoC: full CSV scan)");

            // �N�G��: department �K�{
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var department = query.Get("department");
            if (string.IsNullOrWhiteSpace(department))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Query parameter 'department' is required.");
                return bad;
            }

            // OneLake CSV �� URL
            var csvUrl = Environment.GetEnvironmentVariable("ONELAKE_DFS_FILE_URL");
            if (string.IsNullOrWhiteSpace(csvUrl))
            {
                _logger.LogError("ONELAKE_DFS_FILE_URL environment variable is not set.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // OneLake �� 2023-11-03 �� API �o�[�W�������g�p
            var options = new DataLakeClientOptions(DataLakeClientOptions.ServiceVersion.V2023_11_03);

            // �܂��� Azure CLI ���i���œ���m�F�i�K�v�Ȃ� DefaultAzureCredential �ɐؑցj
            var credential = new AzureCliCredential();

            var fileClient = new DataLakeFileClient(new Uri(csvUrl), credential, options);

            // CSV ���X�g���[���œǂݍ���
            var download = await fileClient.ReadAsync();
            using var stream = download.Value.Content;
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                DetectDelimiter = true,
                BadDataFound = null
            });

            // ��: id,name,age,department,salary �� Employee �Ƀ}�b�v
            csv.Context.RegisterClassMap<EmployeeMap>();

            // �t�B���^�i�啶�������������j
            var deptLower = department.Trim().ToLowerInvariant();
            var employees = new List<Employee>();
            await foreach (var rec in csv.GetRecordsAsync<Employee>())
            {
                if ((rec.Department ?? "").Trim().ToLowerInvariant() == deptLower)
                {
                    employees.Add(rec);
                }
            }

            // ���X�|���X����
            if (employees.Count == 0)
            {
                var okEmpty = req.CreateResponse(HttpStatusCode.OK);
                await okEmpty.WriteAsJsonAsync(new EmployeeResponse
                {
                    Total = 0,
                    Department = department,
                    AverageSalary = 0,
                    Items = new List<Employee>()
                });
                return okEmpty;
            }

            var avg = Math.Round(employees.Average(e => e.Salary));
            var items = employees.Take(MaxItems).ToList();

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(new EmployeeResponse
            {
                Total = employees.Count,
                Department = department,
                AverageSalary = avg,
                Items = items
            });
            return ok;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(ex, "CSV file not found or inaccessible in OneLake.");
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetEmployeesFiltered.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    // CsvHelper �}�b�s���O�iCSV�w�b�_�[�Ɉ�v�j
    private sealed class EmployeeMap : ClassMap<Employee>
    {
        public EmployeeMap()
        {
            Map(m => m.Id).Name("id");
            Map(m => m.Name).Name("name");
            Map(m => m.Age).Name("age");
            Map(m => m.Department).Name("department");
            Map(m => m.Salary).Name("salary");
        }
    }
}
