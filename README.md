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

---

### GET /api/employees?department=<name>

> ⚠️ **PoC implementation** — intended for small-scale data validation only.

Reads a CSV file from OneLake and returns employees filtered by department.

**Intent:**
This endpoint is a sample that demonstrates how Azure Functions can read a CSV file
directly from OneLake and apply a simple in-memory filter. It is useful for verifying
end-to-end connectivity and understanding the basic data flow.

**Constraints:**
- Scans every row of the CSV file on every request (full table scan).
- Not suitable for large datasets — memory and response time grow linearly with file size.
- Not recommended for production use.
- For production workloads, consider using the SQL endpoint (`GET /api/employees/sql`),
  pre-aggregated data, or pushing query logic into the Lakehouse / Warehouse layer.

**Configuration:**
```
ONELAKE_DFS_FILE_URL=https://your-onelake-workspace.dfs.fabric.microsoft.com/your-lakehouse/Files/employees.csv
```

**Usage:**
```bash
func start
curl -sS "http://localhost:7071/api/employees?department=IT"
```

---

### GET /api/employees/sql?department=<name>

Queries employee data via the Fabric SQL endpoint using Entra ID authentication.
Aggregation (COUNT, AVG salary) is pushed down to the database engine, making this
approach efficient regardless of dataset size and suitable for production use.

**Comparison with `GET /api/employees`:**

| | `GET /api/employees` | `GET /api/employees/sql` |
|---|---|---|
| Data source | OneLake CSV file | Fabric SQL endpoint (Lakehouse / Warehouse) |
| Filter execution | In-memory (Azure Functions) | In-database (SQL engine) |
| Scalability | Poor — full CSV scan every request | Good — indexed, engine-side aggregation |
| Use case | PoC / connectivity check | Production workloads |

**Configuration:**
```
SQL_ENDPOINT=<xxx>.datawarehouse.fabric.microsoft.com
SQL_DATABASE=fabricdemo
```

**Usage:**
```bash
func start
curl -sS "http://localhost:7071/api/employees/sql?department=IT"
```

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