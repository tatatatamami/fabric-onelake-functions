# FabricOnelakeFunctions

Azure Functions から Microsoft Fabric OneLake 上の CSV / SQL にアクセスする比較用 PoC サンプルです。

- OneLake 上の CSV をそのまま返すサンプル
- CSV を Azure Functions 側で読み込んで絞り込むサンプル
- Fabric SQL エンドポイントで集計するサンプル

本リポジトリは **PoC 用** です。本番運用を前提とした認証、監視、テスト、性能最適化は最小限です。

## このリポジトリの目的

Azure Functions から OneLake 上の社員データにアクセスする 2 つの方法を比較しやすくすることを目的にしています。

1. **CSV 直接アクセス**
   - OneLake 上の CSV を Azure Functions で読み取る
   - ファイル取得や全件走査ベースの検証向け
2. **SQL エンドポイントアクセス**
   - Fabric SQL エンドポイントに対してクエリを実行する
   - 集計や絞り込みを DB 側に寄せたいケースの比較向け

## 実装済みエンドポイント

| Method | Endpoint | 概要 |
| --- | --- | --- |
| GET | `/api/files/raw` | OneLake 上の CSV をそのまま返す |
| GET | `/api/employees?department=IT` | CSV を Azure Functions 側で全件読み込みし、department で絞り込んで JSON を返す |
| GET | `/api/employees/sql?department=IT` | Fabric SQL エンドポイントで集計し、JSON を返す |

### GET /api/files/raw

OneLake 上の CSV ファイルを `text/csv` でそのまま返します。

```bash
curl -sS http://localhost:7071/api/files/raw | head -n 3
```

### GET /api/employees?department=IT

`department` クエリパラメータは必須です。Azure Functions 側で CSV を読み込み、対象部門のデータを JSON で返します。

```bash
curl -sS "http://localhost:7071/api/employees?department=IT"
```

レスポンス例:

```json
{
  "total": 12,
  "department": "IT",
  "averageSalary": 72000,
  "items": [
    {
      "id": 1,
      "name": "Alice",
      "age": 30,
      "department": "IT",
      "salary": 70000
    }
  ]
}
```

### GET /api/employees/sql?department=IT

`department` を指定すると SQL 側で条件を付けて集計します。未指定の場合は全件を対象に集計します。

```bash
curl -sS "http://localhost:7071/api/employees/sql?department=IT"
```

レスポンス例:

```json
{
  "total": 12,
  "department": "IT",
  "averageSalary": 72000
}
```

## 必要な環境変数

`local.settings.json` の `Values` に以下を設定します。

| Key | 必須 | 説明 |
| --- | --- | --- |
| `ONELAKE_DFS_FILE_URL` | `/api/files/raw`, `/api/employees` で必須 | OneLake 上の CSV ファイル URL |
| `SQL_ENDPOINT` | `/api/employees/sql` で必須 | Fabric SQL Endpoint のホスト名 |
| `SQL_DATABASE` | `/api/employees/sql` で必須 | 接続先データベース名 |

例:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ONELAKE_DFS_FILE_URL": "https://<workspace>.dfs.fabric.microsoft.com/<lakehouse>/Files/sample_employees.csv",
    "SQL_ENDPOINT": "<workspace>.datawarehouse.fabric.microsoft.com",
    "SQL_DATABASE": "<database-name>"
  }
}
```

## ローカル実行

### 前提

- .NET 8 SDK
- Azure Functions Core Tools v4
- Azure CLI

### Azure 認証

ローカル実行は **Azure CLI で認証済みであることを前提** にしています。

```bash
az login
```

必要に応じてサブスクリプションを切り替えます。

```bash
az account set --subscription <SUBSCRIPTION_ID_OR_NAME>
```

### 起動手順

```bash
dotnet restore
dotnet build
func start
```

起動後に `http://localhost:7071` 配下の各エンドポイントを `curl` などで確認できます。

## CSV 直接アクセスと SQL エンドポイント利用の違い

### CSV 直接アクセス / Azure Functions 側で処理

- OneLake 上の CSV を Azure Functions が読み込みます
- 絞り込みや集計のために **CSV を全件走査** します
- ファイルそのものの取得や、シンプルな PoC には向いています

### SQL エンドポイント利用

- 集計や条件指定を SQL 側に委譲します
- Azure Functions からは集計結果だけを取得できます
- データ量が増えた場合の比較検証や、クエリベースのアクセス確認に向いています

## 想定用途

- Azure Functions から OneLake の CSV にアクセスできるかの検証
- CSV 直接アクセスと SQL エンドポイントアクセスの実装比較
- Fabric / OneLake を使った小規模な技術検証

## 非対応事項 / 制約

- 本番運用向けのサンプルではありません
- 認証、監視、例外設計、テストは最小限です
- CSV アクセスは Azure Functions 側でファイルを読み込むため、大きなデータセットには不向きです
- SQL エンドポイント側のテーブル作成やデータ投入はこのリポジトリの対象外です
