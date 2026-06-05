namespace function_onelake.Models;

public sealed class ApiErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}
