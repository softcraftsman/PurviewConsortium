# Purview Consortium — Data Product Sharing Platform

A proof-of-concept platform that enables a consortium of institutions to discover and share **Microsoft Purview Data Products** across organizational boundaries using **OneLake shortcuts** and **Microsoft Fabric**.

## Architecture

```
┌─────────────────────────┐     ┌──────────────────────────┐
│   React SPA (Vite)      │────▶│  ASP.NET Core 8 Web API  │
│   Fluent UI v9           │     │  Microsoft.Identity.Web  │
│   MSAL.js               │     │  Serilog                 │
│   port 5173              │     │  port 7001               │
└─────────────────────────┘     └──────────┬───────────────┘
                                           │
                  ┌────────────────────────┼────────────────────────┐
                  │                        │                        │
                  ▼                        ▼                        ▼
         ┌────────────────┐     ┌───────────────────┐    ┌──────────────────┐
         │  Azure SQL DB  │     │  Azure AI Search  │    │  Purview APIs    │
         │  EF Core 8     │     │  Catalog index    │    │  Data Products   │
         └────────────────┘     └───────────────────┘    └──────────────────┘

         ┌──────────────────────────┐
         │  Azure Functions v4      │
         │  Timer: every 6 hours    │
         │  HTTP: on-demand scan    │
         └──────────────────────────┘
```

## Projects

| Project | Type | Purpose |
|---------|------|---------|
| `PurviewConsortium.Core` | Class Library | Domain entities, enums, interfaces |
| `PurviewConsortium.Infrastructure` | Class Library | EF Core, repositories, Purview/Search services |
| `PurviewConsortium.Api` | ASP.NET Core Web API | REST endpoints, auth, controllers |
| `PurviewConsortium.Functions` | Azure Functions (isolated) | Scheduled & on-demand Purview scanning |
| `PurviewConsortium.Web` | React + Vite SPA | Frontend UI with Fluent UI v9 |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for SQL Server)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- An Azure subscription with:
  - Microsoft Entra ID app registration (multi-tenant)
  - Azure AI Search service
  - Microsoft Purview account(s)

## Quick Start

### 1. Start SQL Server

```bash
docker-compose up -d sqlserver
```

### 2. Configure the API

Copy and edit settings:

```bash
# API — update AzureAd, ConnectionStrings, and AzureAISearch sections
# in src/PurviewConsortium.Api/appsettings.json
```

### 3. Run Database Migrations

```bash
cd src/PurviewConsortium.Infrastructure
dotnet ef database update --startup-project ../PurviewConsortium.Api
```

> If this is the first time, create the initial migration:
> ```bash
> dotnet ef migrations add InitialCreate --startup-project ../PurviewConsortium.Api
> ```

### 4. Run the API

```bash
cd src/PurviewConsortium.Api
dotnet run
```

The API will start on `https://localhost:7001` (or `http://localhost:5001`).

### 5. Run the Frontend

```bash
cd src/PurviewConsortium.Web
cp .env.example .env
# Edit .env with your Entra ID ClientId and TenantId
npm run dev
```

The SPA will start on `http://localhost:5173` and proxy API calls to the backend.

### 6. (Optional) Run Azure Functions

```bash
cd src/PurviewConsortium.Functions
func start
```

## App Registration Setup

1. Go to **Azure Portal → Microsoft Entra ID → App registrations → New registration**
2. Name: `Purview Consortium`
3. Supported account types: **Accounts in any organizational directory (Multi-tenant)**
4. Redirect URI: `http://localhost:5173` (SPA)
5. Configure:
   - **Expose an API**: Set Application ID URI to `api://<client-id>`, add scope `.default`
   - **API permissions**: `Microsoft.Purview` → `DataMap.Read`, `DataMap.Write` (delegated)
   - **App roles**: Create `Consortium.Admin`, `Institution.Admin`, `Data.Consumer`
   - **Certificates & secrets**: Create a client secret for the API backend
6. Each member institution must grant **admin consent** for the app in their tenant

## Environment Variables

### API (`appsettings.json`)

| Setting | Description |
|---------|-------------|
| `AzureAd:ClientId` | App registration client ID |
| `AzureAd:ClientSecret` | App registration secret |
| `AzureAd:TenantId` | `common` for multi-tenant |
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `AzureAISearch:Endpoint` | Azure AI Search endpoint |
| `AzureAISearch:ApiKey` | Azure AI Search admin key |
| `AzureAISearch:IndexName` | Search index name (default: `consortium-catalog`) |

### Frontend (`.env`)

| Variable | Description |
|----------|-------------|
| `VITE_AZURE_CLIENT_ID` | Same app registration client ID |
| `VITE_AZURE_TENANT_ID` | `common` for multi-tenant |

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/catalog/products` | Search/list Data Products |
| GET | `/api/catalog/products/{id}` | Get product detail |
| GET | `/api/catalog/stats` | Dashboard statistics |
| GET | `/api/catalog/filters` | Available filter values |
| POST | `/api/requests` | Submit access request |
| GET | `/api/requests` | List user's requests |
| GET | `/api/requests/{id}` | Get request detail |
| PATCH | `/api/requests/{id}/status` | Update request status |
| DELETE | `/api/requests/{id}` | Cancel request |
| GET | `/api/requests/{id}/fulfillment` | Get fulfillment steps |
| GET | `/api/admin/institutions` | List institutions |
| POST | `/api/admin/institutions` | Register institution |
| PUT | `/api/admin/institutions/{id}` | Update institution |
| DELETE | `/api/admin/institutions/{id}` | Deactivate institution |
| POST | `/api/admin/institutions/{id}/scan` | Trigger institution scan |
| GET | `/api/admin/sync/history` | Get sync history |
| POST | `/api/admin/sync/trigger` | Trigger full scan |

## Key Design Decisions

| # | Decision | Choice |
|---|----------|--------|
| 1 | Backend framework | ASP.NET Core 8 (C#) |
| 2 | Frontend framework | React + Vite SPA |
| 3 | Auth model | Single multi-tenant app registration |
| 4 | OneLake shortcuts | Guided manual (PoC), automate later |
| 5 | Search | Azure AI Search |
| 6 | Notifications | Email only |
| 7 | Tagging | Glossary term "Consortium-Shareable" |
| 8 | Scope | Data Products only |
| 9 | Database | Single DB, no RLS |
| 10 | Purview API | Data Products API (preview) |

## License

Internal / Proof of Concept — Not for production use.
