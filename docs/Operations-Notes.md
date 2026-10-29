# Operations Notes - DEFRA Bank Holidays

How to configure, run, migrate and operate the service. Container, infrastructure and pipeline definitions are not covered here (they are delivered separately).

## Configuration

All settings can be supplied as environment variables, using `__` instead of `:` (for example `BankHolidays__SourceUrl`). Environment variables override `appsettings*.json`. `appsettings.Development.json` is used only for local development and holds a LocalDB connection string.

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:HolidayContext` | SQL Server connection string. **Required**; the app will not start without it. | none (LocalDB in Development) |
| `ApplyMigrationsOnStartup` | Applies migrations when the app starts. Development convenience only. | `false` |
| `BankHolidays:SourceUrl` | Feed that `Home/Update` reads | `https://www.gov.uk/bank-holidays.json` |
| `BankHolidays:SourceTimeoutSeconds` | Timeout for that feed | `30` |
| `KeyVault:Uri` | When set (absolute `https` URL), Key Vault secrets are added as configuration | empty (not used) |
| `ApplicationInsights:ConnectionString` or `APPLICATIONINSIGHTS_CONNECTION_STRING` | Enables Application Insights; the first non-empty value wins | empty (telemetry off) |
| `AzureAd:Instance`, `TenantId`, `ClientId`, `Audience` | Entra ID token validation for the write endpoints | `Instance` set; others empty |
| `Authentication:AllowAnonymousWritesInDevelopment` | Lets write endpoints run without Entra ID, **in Development only** | `false` (`true` in `appsettings.Development.json`) |

### Key Vault

The service authenticates to Key Vault with `DefaultAzureCredential` (managed identity when hosted in Azure; developer sign-in locally). Secret names use `--` for `:`, for example `ConnectionStrings--HolidayContext`. The identity needs permission to read secrets.

### SQL authentication

The data layer uses the Microsoft.Data.SqlClient provider for Entity Framework 6, so the authentication method is chosen by the connection string alone:

| Method | Connection string example |
|---|---|
| Managed identity / Entra ID (Azure SQL) | `Server=tcp:<server>.database.windows.net;Database=DEFRA.BankHolidays;Authentication=Active Directory Default;MultipleActiveResultSets=True` |
| SQL authentication | `Server=<server>;Database=DEFRA.BankHolidays;User ID=<user>;Password=<from Key Vault>;MultipleActiveResultSets=True` |
| Windows integrated (on-premises only) | `Data Source=<server>;Initial Catalog=DEFRA.BankHolidays;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True` |

`Microsoft.Data.SqlClient` encrypts connections by default. Add `TrustServerCertificate=True` (or `Encrypt=False`) only where the server certificate is not trusted, such as LocalDB. The managed identity must be a database user with read/write permission on the bank holiday tables (and, for the migration job, permission to create tables). The EF6 retry strategy for transient Azure SQL errors is enabled.

The database initialiser is disabled: the app never creates or changes the schema by itself.

## Health endpoints

| Path | Meaning |
|---|---|
| `/health/live` | The process is running (no dependencies checked) |
| `/health/ready` | The database can be reached (`SELECT 1`, 5-second command timeout). Returns `503` when it cannot; no failure detail is returned. |

Both are anonymous. Cold-start latency of the first data request has not been measured under Azure; set start-up/readiness probe timeouts with that in mind.

## Database schema and migrations

| Command | Effect |
|---|---|
| `dotnet DEFRA.BankHolidays.dll --migrate` | Applies pending migrations to the database in `ConnectionStrings:HolidayContext`, then exits (exit code `0` on success, `1` on failure) |
| `dotnet DEFRA.BankHolidays.dll --script <path>` | Writes the T-SQL for all migrations to `<path>`, then exits |

`Database\001_InitialCreate.sql` is the script generated from the single existing migration. It was verified to create exactly the same schema (columns, keys, indexes, migration-history row) as `--migrate`. Apply it with any SQL tool that can authenticate with Entra ID, or use `--migrate` from a one-off job. Run migrations once per deployment, not from every replica; leave `ApplyMigrationsOnStartup` off outside local development (the app logs a warning if it is on in another environment).

## Refreshing the data

`POST /Home/Update` reads `BankHolidays:SourceUrl` on the server and replaces all bank holiday data in one transaction. It returns `200` on success, `502` when the source is unavailable or returns invalid data, `409` while another refresh is running in the same instance, and `500` for a save failure (details are logged, never returned).

`POST /Home/Refresh` accepts JSON in a form field named `data` and replaces the data with it. It exists for compatibility and has the same protection and error handling.

Both require an Entra ID access token that carries the **`BankHolidays.Refresh`** app role. The scheduled refresh (and any operator) must present such a token:

```powershell
.\Scripts\Powershell\DEFRA.BankHolidays.Update.ps1 -BaseUrl https://<host> -AccessToken <token>
```

Without `-BaseUrl` the script keeps the previous on-premises default (`http://localhost/defra.bankholidays`). The home page no longer contains a refresh button; refresh runs from the scheduled process.

The refresh guard only coordinates requests within one instance. Run the scheduled refresh once (not per replica).

> Fixed in this release: every refresh previously left the old division and event rows behind (the child tables grew by one full copy per refresh). Refresh now deletes them in the same transaction, which also removes rows left behind by earlier refreshes the first time it runs against an existing database. If operations want to keep a copy of the old rows, take a backup before the first refresh with this version.

## Authentication and authorization

- **Read endpoints** (`GET api/values...`, `/swagger`, health) are anonymous; the API contract is unchanged.
- **Write endpoints** (`Home/Update`, `Home/Refresh`) use the `BankHolidays.Refresh` policy: a valid Entra ID access token for this API **and** the `BankHolidays.Refresh` app role.
- The app **fails closed**: outside Development it will not start unless `AzureAd:TenantId` and `AzureAd:ClientId` are set. In Development it starts without them only when `Authentication:AllowAnonymousWritesInDevelopment` is `true`; that setting is ignored in every other environment.

Entra ID objects that must exist (to be created by the tenant owner):

1. An app registration for this API (its application ID is `AzureAd:ClientId`; the tenant is `AzureAd:TenantId`; `AzureAd:Audience` only if the token audience differs from the default).
2. An **app role** with value `BankHolidays.Refresh`, allowed member types **Applications** (for the scheduled job's managed identity) and/or **Users/Groups** (for operators).
3. An assignment of that role to the job's managed identity and to any operator group.

Real Entra token validation was not exercised in this environment (no tenant available); the authorization policy is covered by automated tests that substitute the token validation step.

## Telemetry

Set `ApplicationInsights__ConnectionString` or `APPLICATIONINSIGHTS_CONNECTION_STRING` to enable telemetry. Application Insights SDK 3.2.0 does not expose a cloud role name or request filter through its options:

- Set the role name with the standard OpenTelemetry variable `OTEL_SERVICE_NAME=bank-holidays` (not verified against a live resource).
- Excluding `/health/*` requests from telemetry is **not** configured; this is a follow-up.
- The SDK's `ApplicationInsightsServiceOptions.Credential` can be used to authenticate telemetry ingestion with Entra ID; it is not enabled.

## Local development

1. Install SQL Server LocalDB (or point `ConnectionStrings__HolidayContext` at another SQL Server).
2. Set `ASPNETCORE_ENVIRONMENT=Development` so `appsettings.Development.json` is loaded.
3. Create the schema once with `dotnet run --project DEFRA.BankHolidays/DEFRA.BankHolidays -- --migrate`, then start the app with `dotnet run --project DEFRA.BankHolidays/DEFRA.BankHolidays`.
4. `appsettings.Development.json` allows anonymous writes in Development so `Home/Update` can be called without a token.

## Known follow-ups

- Exclude health probes from telemetry; set the role name in a deployed environment.
- Real Entra token validation and managed-identity SQL access need a deployed environment to verify.
- Swashbuckle has no bearer-token definition; use the script (or any HTTP client) with a token for write calls.
- Client-side libraries under `wwwroot` (jQuery 1.10.2, Bootstrap 3.0.0, Modernizr 2.6.2, respond.js) are old and unchanged by this work; the layout still loads Font Awesome from a public CDN.
- Swagger UI is enabled in every environment and `AllowedHosts` is `*`.
