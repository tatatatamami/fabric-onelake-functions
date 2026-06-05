# FabricOnelakeFunctions

Azure Functions application for accessing OneLake CSV files.

## Endpoints

### GET /api/files/raw

Returns CSV files directly from OneLake with appropriate content-type headers.

**Features:**
- Direct stream response from OneLake
- Content-Type: text/csv; charset=utf-8
- Proper error handling (403/404/500)
- Uses DefaultAzureCredential for authentication

**Configuration:**
Set the `ONELAKE_DFS_FILE_URL` environment variable to point to your OneLake CSV file:
```
ONELAKE_DFS_FILE_URL=https://your-onelake-workspace.dfs.fabric.microsoft.com/your-lakehouse/Files/your-file.csv
```

**Usage:**
```bash
func start
curl -sS http://localhost:7071/api/files/raw | head -n 3
```

## Error response format

All endpoints return errors in the following JSON format:

```json
{
  "code": "BadRequest",
  "message": "Query parameter 'department' is required."
}
```

Representative status mapping:

- `400 BadRequest`: request validation errors (example: missing `department`)
- `404 NotFound`: target data/file not found
- `500 ServerError`: server-side configuration or unexpected errors
- `503 DependencyUnavailable`: OneLake or SQL access/authentication failures

## Development

### Prerequisites
- .NET 8.0 SDK
- Azure Functions Core Tools
- Azure CLI (for authentication)

### Build and Run
```bash
dotnet build
func start
```