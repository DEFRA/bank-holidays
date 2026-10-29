# DEFRA Bank Holidays - Detailed ACA Cloud-Readiness Assessment

- Report ID: `20261027070103`
- Target: **Azure Container Apps (ACA)**
- Producer: **.NET AppCAT CLI** (`dotnet-appcat` 1.0.1127), report format 1.0.0
- Analysis window: 2026-10-27T07:01:03.588Z -> 2026-10-27T07:01:13.424Z
- Command: `appcat analyze <2 projects> --source Solution --target Any --serializer APPMODJSON --code --privacyMode Restricted --non-interactive`
- Raw report: `.github/modernize/assessment/reports/report-20261027070103/report.json`
- Projects analyzed: 2 (`DEFRA.BankHolidays`, `DEFRA.BankHolidays.Tests`), both `net10.0`
- Distinct issue types: 4
- Total incidents: 12
- Total remediation effort (story points): 22
- Related report: [ACA-Readiness-Assessment.md](./ACA-Readiness-Assessment.md) (manual assessment against the CAF documents, 2026-10-27)

## Executive Summary

| Question | Answer |
|---|---|
| Does AppCAT find blockers for ACA? | **No.** 0 Mandatory incidents. |
| Can the app be deployed to ACA as-is? | **No.** AppCAT's static rules do not see several deployment-critical gaps (section "Issues AppCAT cannot detect"). |
| Is the app migratable to ACA? | **Yes.** It is already .NET 10 / ASP.NET Core, stateless, and has no local-file or session dependencies. |
| Total remediation effort | AppCAT: **22 story points** (almost all configuration/connection-string work). Manual findings add container, security and operations work (indicative 6-10 person-days overall, see the related report). |

Overall rating: **Amber - ready to start ACA remediation; not ready to deploy.**

All four AppCAT rules produced **identical severity and effort for all 8 targets** (App Service Windows/Linux, AKS Linux/Windows, ACA, App Service Container Linux/Windows, App Service Managed Instance). The findings are not specific to ACA, and no target is penalised more than another.

## Severity / Priority Legend

| Severity | Priority | Meaning |
|---|---|---|
| Mandatory | **P0 - Must have** | Blocks migration - must fix before deployment |
| Potential | **P1 - Best Practice** | High priority - likely to cause issues in Azure/ACA if unaddressed |
| Optional | **P2 - Good to have** | Medium/low priority - best-practice hardening recommendations |
| Information | - | Informational only, no action required |

Priorities for items in the section "Issues AppCAT cannot detect" are **assessor-assigned** (not tool output) using the same P0/P1/P2 scale, with an additional assessor severity (Critical / High / Medium / Low).

## AppCAT Results Summary

| Metric | Value |
|---|---:|
| Projects | 2 |
| Issues (distinct rules) | 4 |
| Incidents | 12 |
| Effort (story points) | 22 |
| Mandatory incidents | 0 |
| Potential incidents | 11 |
| Optional incidents | 1 |
| Information incidents | 0 |

| Category | Incidents |
|---|---:|
| Configuration | 6 |
| Connection | 4 |
| Scale | 1 |
| Local | 1 |

| Project | Issues | Incidents | Story points |
|---|---:|---:|---:|
| DEFRA.BankHolidays | 4 | 12 | 22 |
| DEFRA.BankHolidays.Tests | 0 | 0 | 0 |

## P0 - Mandatory (Must have) - 0 incidents, 0 effort points

No mandatory/blocking issues were reported by AppCAT for the ACA target.

> Important: AppCAT reporting zero mandatory issues does **not** mean the app is deployable. Three deployment-critical gaps exist that these static rules do not detect (SQL authentication model, missing container assets, unauthenticated write endpoints). They are listed as P0 in the section "Issues AppCAT cannot detect" below.

## P1 - Potential (Best Practice) - 11 incidents, 19 effort points

### Connection.0001 - Connection string is detected
*Category:* Connection | *Incidents:* 4 | *Effort/incident:* 3 | *Total effort:* 12

> Connection string detected. It may not be available when your app is migrated to Azure. Review and ensure it works from Azure. If you are connecting to a database, you might need to migrate the database to Azure (Azure SQL Database, Azure SQL Managed Instance, or SQL Server on Azure VMs). To assess database migration effort, you can use Azure Migrate.

**References:**
- [Migrate SQL Server database to Azure](https://go.microsoft.com/fwlink/?LinkID=2251731)
- [Azure SQL Managed Instance](https://go.microsoft.com/fwlink/?LinkID=2251613)
- [Azure Migrate](https://go.microsoft.com/fwlink/?linkid=2252410)

| Project | Location | Detail | Occurrences |
|---|---|---|---|
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.json` | `ConnectionStrings:HolidayContext` | 1 |
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.Production.json` | `ConnectionStrings:HolidayContext` | 1 |
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.SIT.json` | `ConnectionStrings:HolidayContext` | 1 |
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.UAT.json` | `ConnectionStrings:HolidayContext` | 1 |

*Detail shown is from source verification; the report was generated in Restricted privacy mode, which omits snippets.*

### Configuration.0003 - Local application configuration detected
*Category:* Configuration | *Incidents:* 6 | *Effort/incident:* 1 | *Total effort:* 6

> Local application configuration detected. Azure App Configuration can be used as a centralized, cloud-based service for application settings currently stored in local configuration files, enabling externalized settings, centralized management across environments, and dynamic refresh with controlled access.

**References:**
- [What is Azure App Configuration?](https://learn.microsoft.com/azure/azure-app-configuration/overview)
- [Configuration management with Azure App Configuration](https://aka.ms/appconfig/configuration-management)

| Project | Location | Occurrences |
|---|---|---|
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.json` | 3 |
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.Production.json` | 1 |
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.SIT.json` | 1 |
| DEFRA.BankHolidays | `DEFRA.BankHolidays\appsettings.UAT.json` | 1 |

### Local.0006 - Hardcoded URLs detected
*Category:* Local | *Incidents:* 1 | *Effort/incident:* 1 | *Total effort:* 1

> Your application has hardcoded URLs that might correspond to services that are not accessible on Azure. Review and ensure that the application is attempting to access URLs that would be available from Azure. If your application depends on services that run on-premises, connect Azure and local networks with ExpressRoute, Hybrid Connections, or VPN.

**References:**
- [ExpressRoute](https://go.microsoft.com/fwlink/?LinkID=2250577)
- [Hybrid Connections](https://go.microsoft.com/fwlink/?LinkID=2242588)
- [Connect an on-prem network](https://go.microsoft.com/fwlink/?LinkID=2242591)

| Project | Location | Detail | Occurrences |
|---|---|---|---|
| DEFRA.BankHolidays | `DEFRA.BankHolidays\Controllers\HomeController.cs` | `https://www.gov.uk/bank-holidays.json` (line 10) | 1 |

## P2 - Optional (Good to have) - 1 incident, 3 effort points

### Scale.0001 - Static content detected
*Category:* Scale | *Incidents:* 1 | *Effort/incident:* 3 | *Total effort:* 3

> Static content detected. Serving static content directly from the application may increase cost, hurt performance, require redeployment for content changes, and carry security risks. Consider moving static content to Azure Blob Storage with Azure CDN.

**References:**
- [Azure Blob Storage](https://go.microsoft.com/fwlink/?linkid=2250574)
- [Azure CDN](https://go.microsoft.com/fwlink/?linkid=2250392)

| Project | Location | Detail | Occurrences |
|---|---|---|---|
| DEFRA.BankHolidays | `DEFRA.BankHolidays\DEFRA.BankHolidays.csproj` | `wwwroot` (19 files, about 1.5 MB) | 1 |

## Triaged Issue List with Suggested Fixes (verified against source)

| Priority | Severity | Rule / Issue | File | Occ. | Suggested Fix | Remarks |
|---|---|---|---|---:|---|---|
| P0 | High | Connection string - Windows Integrated Security (`Connection.0001`) | `appsettings.json`, `.SIT.json`, `.UAT.json`, `.Production.json` | 4 | All four connection strings use `Integrated Security=True` against on-premises hosts (`.`, `D3VMDVWDEV002`, `D3VMPPWSQL003`, `D3VMPRWSQL003`). Move to Azure SQL with **managed identity** (token-based connection) or SQL auth with the secret in **Key Vault**; supply the value through ACA configuration, not the committed files. | **Genuine.** AppCAT rates it only "potential", but a Linux container cannot use Integrated Security. The app uses EF6 on `System.Data.SqlClient` 4.8.6 (no Entra connection-string options), so managed identity needs a token-based connection or an EF Core migration. Effort: Medium. |
| P1 | Medium | Local configuration (`Configuration.0003`) | the 4 `appsettings*.json` files | 6 | For ACA, use environment variables / ACA secrets / Key Vault references. **Azure App Configuration** is optional and only worthwhile if central or dynamic settings are wanted. | Real but low effort. The app already uses `IConfiguration`, so env-var overrides (`ConnectionStrings__HolidayContext`) work without code change - confirmed by the runtime smoke test in the related report. |
| P1 | Low | Hardcoded URL - gov.uk feed (`Local.0006`) | `Controllers\HomeController.cs:10` | 1 | Move `https://www.gov.uk/bank-holidays.json` to configuration (e.g. `BankHolidays:SourceUrl`); allow outbound HTTPS from the ACA environment. | Public internet endpoint, reachable from ACA unless egress is restricted. The same URL is also hard-coded in `Views\Home\Index.cshtml` (lines 9 and 41), which AppCAT did not flag. Effort: Low. |
| P2 | Low | Static content (`Scale.0001`) | `DEFRA.BankHolidays.csproj` (`wwwroot`) | 1 | Serve from the container for now; consider Blob Storage + CDN only if traffic/cost justifies it. | Low value: about 1.5 MB, 19 files, a small internal service. The libraries in `wwwroot` are old (see M-11) - upgrading them matters more than relocating them. **Defer.** |

**Summary:** of AppCAT's 22 story points, about 12 (connection strings) and 6 (configuration) are one piece of work - externalising environment configuration and choosing the SQL authentication model. The remaining 4 points (hardcoded URL, static content) are low-value cleanup.

## Issues AppCAT Cannot Detect (manual review and runtime checks)

These come from source review, the CAF migration documents and a runtime smoke test, not from the AppCAT rule set. They are assessor-rated. Details and evidence are in [ACA-Readiness-Assessment.md](./ACA-Readiness-Assessment.md).

### P0 - Must fix before ACA deployment

| ID | Severity | Issue | Evidence | Recommended fix | Effort |
|---|---|---|---|---|---|
| M-1 | **Critical** | **No container, IaC or CI/CD assets.** No Dockerfile, `.dockerignore`, ACA definition, Bicep/Terraform or pipeline. | Repository inventory; `reports/bank-holidays.md` and CAF documents agree. | Add a multi-stage Dockerfile (Linux, .NET 10 SDK -> ASP.NET runtime), a build/push pipeline to Azure Container Registry, and ACA infrastructure as code. | 1.5-2 days |
| M-2 | **High** | **Write endpoints are unauthenticated.** `POST /Home/Refresh` accepts arbitrary client-supplied JSON and replaces the whole dataset; `POST /Home/Update` also runs anonymously. | Runtime smoke test: data replaced with no credentials. | Entra ID (`Microsoft.Identity.Web`, JWT bearer) with an app role (e.g. `BankHolidays.Refresh`) on write endpoints; prefer server-side fetch (`Update`) and retire client-supplied `Refresh`; restrict ingress if internal-only. | 1-2 days |
| M-3 | **High** | **SQL authentication model for Azure** (see Connection.0001 above). | Section "Triaged Issue List", first row. | Choose and implement managed identity or Key Vault-backed SQL auth. | 1-2 days |

### P1 - Best practice, likely to cause problems on ACA

| ID | Severity | Issue | Evidence | Recommended fix | Effort |
|---|---|---|---|---|---|
| M-4 | High | **Application Insights will not enable with the Azure-standard setting.** The code gates on `ApplicationInsights:ConnectionString` only; `APPLICATIONINSIGHTS_CONNECTION_STRING` would be ignored. All configs ship the value blank. | `Program.cs` (code reading; not tested against a live resource). | Supply `ApplicationInsights__ConnectionString` via ACA secret/Key Vault, or honour the standard variable; set a cloud role name; exclude health probes from telemetry. | 0.5 day |
| M-5 | Medium | **No health endpoints.** `GET /health` returns 404. | Runtime smoke test. | Add liveness and readiness (database reachable) endpoints and configure ACA probes. The first DB request after a cold start was slow in testing (cause unconfirmed) - size probe timeouts accordingly. | 0.5 day |
| M-6 | Medium | **Schema migrations run inside the web process when `ApplyMigrationsOnStartup=true`** (EF6 `DbMigrator`). Several replicas starting together can race; with the flag off (default), nothing applies the schema. | `Program.cs`; migration applied successfully on a scratch DB in testing. | Run migrations as a single-run pipeline step or ACA Job; keep the flag off in ACA. | 0.5-1 day |
| M-7 | Medium | **Scheduled refresh has no ACA equivalent.** `Scripts\Powershell\DEFRA.BankHolidays.Update.ps1` posts to `http://localhost/defra.bankholidays/home/update`. | Script content. | Replace with an ACA scheduled Job (cron) calling the authenticated update operation; run once, not per replica. | 0.5-1 day |
| M-8 | Medium | **Consumer endpoint will change.** `rpa-inspections-workbench` calls `http://d2vmbwa001/defra.bankholidays/api/Values/workingdays` (HTTP, hard-coded host and virtual directory). | `appsettings.json` of the workbench. | Publish the ACA address, update the consumer setting, and agree the consumer authentication approach if read access is restricted. | 0.5 day |
| M-9 | Medium | **Internal error text returned to callers.** `Refresh` returns `ex.Message` with HTTP 200. | Runtime smoke test (parser error text returned). | Return a generic error with a proper status code; log the detail. | 0.25 day |

### P2 - Good to have

| ID | Severity | Issue | Recommended fix |
|---|---|---|---|
| M-10 | Medium | Swagger UI enabled in every environment; `AllowedHosts` is `*`. | Gate Swagger by environment/auth; restrict allowed hosts to the ACA hostnames. |
| M-11 | Medium | Vendored client libraries are old (jQuery 1.10.2, Bootstrap 3.0.0, Modernizr 2.6.2, respond.js); the layout also loads Font Awesome from a public CDN. NuGet audit does not cover these. The 6 October AppCAT security run recorded nine medium advisories for Bootstrap and jQuery. (CAF task T-051, still open.) | Upgrade/replace the libraries; self-host or pin the CDN asset with integrity checks. |
| M-12 | Low | Forwarded headers not configured; ACA terminates TLS at ingress. | Add forwarded-header handling if HTTPS redirection, absolute URLs or secure cookies are introduced. |
| M-13 | Low | `Views\Home\Index.cshtml` has `if (response = "Ok")` (assignment, not comparison), so the failure alert can never show. The browser also calls gov.uk directly, then posts the JSON back. | Fix with the client-library work; see M-2 for the server-side fetch. |
| M-14 | Low | Test depth: 7 mock-based unit tests; no HTTP-pipeline or database tests; coverage not measured. | Add `WebApplicationFactory` tests (JSON contract, `204`, legacy `{id}` route, errors, authorization) and a SQL-backed integration test. |
| M-15 | Low | Default log level `Information`. | Review once probes and telemetry are in place. |

## Azure and Microsoft Standards

| Standard | Status | Notes |
|---|---|---|
| **Azure Key Vault** | Not implemented | No Key Vault usage. Connection strings are committed in `appsettings*.json`; the App Insights connection string is blank. Use ACA secrets with Key Vault references via managed identity. |
| **Application Insights** | Partially implemented | `Microsoft.ApplicationInsights.AspNetCore` 3.2.0 is referenced but only registered when the connection string is set (see M-4). |
| **Microsoft Entra ID** | Not implemented | Anonymous app; write endpoints open (M-2). Managed identity for Azure SQL also not in place (M-3). |
| **Azure Storage account** | Not required | No file, blob, queue or table usage. Static content (Scale.0001) does not justify it at this size. |

## Build, Test and Package Validation

| Check | Result |
|---|---|
| Release build (`dotnet build -c Release --no-incremental`) | PASS - 0 warnings, 0 errors |
| Unit tests (`dotnet test -c Release`) | PASS - 7 passed, 0 failed, 0 skipped, exit code 0 |
| Vulnerable NuGet packages (direct + transitive) | None |
| Deprecated NuGet packages | None |
| AppCAT-triggered project builds | Both projects built successfully during analysis |
| Runtime smoke test (Kestrel + scratch LocalDB, synthetic data) | Documented API contract confirmed; `/health` = 404 |

Not verified: a container image build (Docker daemon not running), Azure SQL, and any deployed ACA behaviour.

## Suggested Remediation Order for ACA Deployment

1. **Container baseline (M-1, M-5):** Dockerfile, ACR/ACA definition, health endpoints.
2. **Configuration and data access (Connection.0001, Configuration.0003, M-3, M-6):** externalise settings, choose SQL authentication model, move migrations to a pipeline step/Job.
3. **Observability (M-4):** App Insights connection string via Key Vault; fix the registration gate.
4. **Security (M-2, M-9, M-10):** Entra protection for write operations; remove client-supplied `Refresh`; generic errors; restrict Swagger/hosts.
5. **Integration (M-7, M-8, Local.0006):** scheduled refresh Job, consumer URL update, gov.uk URL to configuration.
6. **Quality (M-11, M-14):** client-library upgrade, pipeline-level tests, then deployed ACA validation.

## Notes and Limitations

- AppCAT ran in **Restricted** privacy mode, so incident locations are file-level only and code snippets are omitted. Detail columns above come from source verification.
- AppCAT is a static rule engine for portability issues. It does not assess authentication, container assets, health endpoints, scheduling or runtime behaviour, which is why section "Issues AppCAT cannot detect" exists.
- The AppCAT rule effort values (story points) are the tool's own; effort estimates for manual items are the assessor's indicative values (+/- 40%).
- The earlier AppCAT report of 6 October 2026 (`docs/Report_20261006091020.md`) assessed the **pre-upgrade** net452 code (23 incidents, 63 story points, including a Newtonsoft.Json CVE). This run reflects the current .NET 10 code; the framework, Web API and CVE findings no longer appear.
- No source or configuration files were modified. The `dotnet-appcat` tool was installed to an isolated tool path outside the repository.
