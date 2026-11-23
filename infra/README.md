# Bicep handoff - DEFRA Bank Holidays

Repository-only infrastructure handoff, not a deployment or production approval. The receiving team supplies
approved parameter values and tests manually in its own subscription. No regional, quota, Azure Policy, what-if,
cost, recovery, compliance or deployed-runtime checks have been performed here.

The layout, naming, network and monitoring model mirror [people-api/infra](../../people-api/infra) (same AVM module
pins, private-only networking, two-stage foundation/application deployment). The difference is the service set,
derived from what the Bank Holidays code actually uses.

## Services used by the application and where they come from

| Application dependency (evidence) | Azure service | Provisioned in |
|---|---|---|
| EF6 `HolidayContext`, `ConnectionStrings:HolidayContext`, `/health/ready` `SELECT 1` | **Azure SQL Database** (serverless, Entra-only auth, private endpoint) | `main.bicep` |
| Managed-identity SQL access (`Authentication=Active Directory Managed Identity`) | **User-assigned managed identity**; contained DB user via [Grant-DatabaseAccess.sql](./Grant-DatabaseAccess.sql) | `main.bicep` + DBA step |
| `KeyVault:Uri` (`AddOptionalKeyVault`, `DefaultAzureCredential`) | **Key Vault** (RBAC, purge protection, private endpoint, identity = Secrets User) | `main.bicep` |
| `AddApplicationInsightsTelemetry` | **Application Insights** + **Log Analytics** + Azure Monitor Private Link (AMPLS) | `main.bicep` |
| `AzureAd:*` + `BankHolidays.Refresh` role (`AddMicrosoftIdentityWebApi`) | **Microsoft Entra ID** app registration and role | [entra/](./entra) scripts (owner step) |
| Container hosting (net10.0, port 8080, `/health/live`, `/health/ready`) | **Container Apps** (internal environment) + **ACR** (Premium, private) | `main.bicep` / `app.bicep` |
| `BankHolidays:SourceUrl` (outbound HTTPS to gov.uk) | No Azure service; needs outbound HTTPS from the ACA subnet | see "Egress" |

Compared with People API: **added** Azure SQL (+ its private endpoint and diagnostic settings) and the SQL
parameters; **removed** the Microsoft Graph registration/secret parameters, CORS and app-role-name parameters (Bank
Holidays has none); the role value is the code constant `BankHolidays.Refresh`. No Blob Storage account or Data
Protection key ring is created (see "Known limits").

## Files

- [main.bicep](./main.bicep): foundation - network, private DNS/endpoints, identity, ACR, Key Vault, SQL server and
  database, monitoring and the ACA environment. **No application starts.**
- [app.bicep](./app.bicep): application activation (same names/identity as the foundation).
- [Grant-DatabaseAccess.sql](./Grant-DatabaseAccess.sql): DBA script giving the runtime identity `db_datareader` +
  `db_datawriter` only.
- [entra/New-BankHolidaysEntraRegistration.ps1](./entra/New-BankHolidaysEntraRegistration.ps1) and
  [entra/Grant-BankHolidaysRole.ps1](./entra/Grant-BankHolidaysRole.ps1): app registration with the
  `BankHolidays.Refresh` role and assignment of the scheduled job's identity (plan-only unless `-Apply`).
- [Test-Templates.ps1](./Test-Templates.ps1): local compile, negative tests and parameter-file validation; no Azure calls.
- `*.parameters.example.json`: deliberately incomplete (`null` = unknown). Copy outside Git and fill in.

```powershell
.\infra\Test-Templates.ps1 -BicepPath "$env:USERPROFILE\.azure\bin\bicep.exe"
.\infra\Test-Templates.ps1 -BicepPath 'C:\tools\bicep.exe' `
  -FoundationParameters 'C:\private\bank-holidays\foundation.parameters.json' `
  -ApplicationParameters 'C:\private\bank-holidays\application.parameters.json'
```

## Foundation inputs (in addition to People API's network/ACR/monitoring inputs)

| Inputs | Contract |
|---|---|
| `sqlAdminName`, `sqlAdminObjectId`, `sqlAdminPrincipalType` | Entra administrator (group recommended). SQL authentication is disabled; the runtime identity must not be the admin. |
| `databaseName` | `DEFRA.BankHolidays` (value used by the app's connection string). |
| `sqlMaxVcores`, `sqlMinVcores`, `sqlMaxSizeGiB`, `sqlZoneRedundant` | Serverless General Purpose sizing; explicit, no defaults. Auto-pause is disabled. |
| `sqlBackupRedundancy`, `sqlPointInTimeRetentionDays`, `sqlLongTermRetention` | Explicit backup policy (`PT0S` durations = no LTR). |

## Application inputs

`location`/`prefix`/`environmentName`/`tags` must match the foundation. Others: `imageRepository` + `imageDigest`
(immutable `sha256:` digest), `entraTenantId`/`entraClientId` (this API's registration), `databaseName` (same as
foundation), `holidaySourceUrl` (https) / `holidaySourceTimeoutSeconds`, `containerSize`, replica bounds,
`concurrentRequests`, `terminationGracePeriodSeconds`.

The app receives: a passwordless SQL connection string (`ConnectionStrings__HolidayContext`, managed identity),
`KeyVault__Uri`, `AZURE_CLIENT_ID` (so `DefaultAzureCredential` selects the user-assigned identity), the
Application Insights connection string, `OTEL_SERVICE_NAME=bank-holidays`, `AzureAd__*` and `BankHolidays__*`.
`ApplyMigrationsOnStartup` is forced to `false`.

## Deployment order

1. Foundation: `az deployment group what-if` then `create` with `main.bicep`; capture outputs.
2. Owner bootstrap from an approved private-network path:
   1. Build and push the image to ACR (**this repository has no Dockerfile yet**; one is needed, listening on 8080).
   2. Run `entra/New-BankHolidaysEntraRegistration.ps1 -Apply`; use its tenant/client IDs as `entraTenantId`/`entraClientId`.
      Grant the scheduled job's identity with `entra/Grant-BankHolidaysRole.ps1`; assign operators in the portal.
   3. As the SQL Entra administrator, run `Grant-DatabaseAccess.sql` (identity name = output `managedIdentityName`).
   4. Create the schema with an administrative identity: `DEFRA.BankHolidays.dll --migrate`, or `--script <file>` and
      apply it. The runtime identity has no DDL rights by design.
3. Application: `what-if` then `create` with `app.bicep`. Verify `/health/ready` (database reachable), an anonymous
   `GET api/values`, and that `POST /Home/Update` returns 401/403 without a `BankHolidays.Refresh` token.

## Known limits and decisions for the owner

- **Replicas:** `maxReplicas` is 1 in the example. The refresh guard is per-instance and the anonymous
  `Home/ManualRefresh` antiforgery keys are not persisted/shared (the app does not configure ASP.NET Core Data
  Protection), so a second replica can reject valid antiforgery tokens. Raising it needs an app change (shared key ring,
  as in `rpa-mts-inspections`). Do not schedule the refresh per replica.
- **Application Insights local auth is left enabled** (People API disables it). The app does not set
  `ApplicationInsightsServiceOptions.Credential`, so with local auth disabled telemetry would be rejected. Ingestion is
  still private-only. The identity already holds Monitoring Metrics Publisher; flip `disableLocalAuth` to `true` in
  `main.bicep` (and the matching check in `Test-Templates.ps1`) once the app authenticates telemetry.
- **Egress:** the feed `https://www.gov.uk/bank-holidays.json` is fetched server-side. The ACA subnet NSG here
  does not restrict outbound traffic; if a firewall/UDR/NAT is added, allow outbound 443 to `www.gov.uk`. The
  layout still loads Font Awesome from a public CDN in the browser.
- **Internal ingress:** like People API, the environment is internal. The home-page Refresh button and Swagger
  are reachable only from connected private networks; supply private DNS for the environment's default domain
  (created here for the VNet).
- **Key Vault** holds no secrets by default (the SQL connection is passwordless). It is provisioned because the
  app's configuration provider is wired to it and for future secrets (`ConnectionStrings--HolidayContext`
  style names).
- Not validated: SQL private DNS resolution, managed-identity token flow to Azure SQL and Entra token
  validation (all need a deployed environment, as noted in `docs/Operations-Notes.md`).

## Module pins

Identical to People API: identity 0.6.0; VNet 0.10.2; Log Analytics 0.16.1; Application Insights 0.8.0; ACR
0.13.1; Key Vault 0.14.2; ACA environment 0.16.0; ACA application 0.23.0; plus **SQL server 0.22.1** (as in
`rpa-mts-inspections`). AVM usage telemetry is disabled.
