using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace function_onelake.Endpoints;

public class GetFilePassthrough
{
    private readonly ILogger<GetFilePassthrough> _logger;

    public GetFilePassthrough(ILogger<GetFilePassthrough> logger)
    {
        _logger = logger;
    }

    [Function("GetFilePassthrough")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "files/raw")] HttpRequestData req)
    {
        _logger.LogInformation("Processing request for OneLake CSV file.");

        try
        {
            // ���ϐ����� OneLake �̃t�@�C�� URL ���擾
            var oneLakeFileUrl = Environment.GetEnvironmentVariable("ONELAKE_DFS_FILE_URL");
            _logger.LogInformation("ONELAKE_DFS_FILE_URL = {Url}", oneLakeFileUrl);

            if (string.IsNullOrEmpty(oneLakeFileUrl))
            {
                _logger.LogError("ONELAKE_DFS_FILE_URL environment variable is not set.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // OneLake ���v������ API �o�[�W�����𖾎��i2023-11-03�j
            var dlOptions = new DataLakeClientOptions(DataLakeClientOptions.ServiceVersion.V2023_11_03);

            // �܂��� Azure CLI �Ɠ������i���œ������Ă݂�i����m�F�p�j
            var credential = new AzureCliCredential();

            // FileClient �𐶐�
            var fileClient = new DataLakeFileClient(new Uri(oneLakeFileUrl), credential, dlOptions);

            // �t�@�C�����݊m�F�i�C�ӁA�Ȃ��Ă� Read ���� 404 ���E����j
            var existsResponse = await fileClient.ExistsAsync();
            if (!existsResponse.Value)
            {
                _logger.LogWarning("File not found at URL: {FileUrl}", oneLakeFileUrl);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            // �t�@�C�����_�E�����[�h
            var downloadResponse = await fileClient.ReadAsync();

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", "text/csv; charset=utf-8");
            await downloadResponse.Value.Content.CopyToAsync(resp.Body);

            _logger.LogInformation("Successfully retrieved CSV file from OneLake.");
            return resp;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError(ex, "Access forbidden when trying to access OneLake file.");
            return req.CreateResponse(HttpStatusCode.Forbidden);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError(ex, "File not found in OneLake.");
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure request failed with status {Status}: {Message}", ex.Status, ex.Message);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while processing OneLake file request.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}
