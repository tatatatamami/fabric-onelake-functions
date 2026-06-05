using System.Net;
using function_onelake.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace function_onelake.Http;

public static class HttpRequestDataExtensions
{
    public static async Task<HttpResponseData> CreateErrorResponseAsync(
        this HttpRequestData req,
        HttpStatusCode statusCode,
        string code,
        string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Code = code,
            Message = message
        });
        return response;
    }
}
