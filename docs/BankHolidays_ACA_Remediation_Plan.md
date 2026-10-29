# DEFRA Bank Holidays - ACA Remediation Plan (P1 issues)

**Date:** 2026-10-27
**Application:** `bank-holidays/DEFRA.BankHolidays` (ASP.NET Core, `net10.0`, EF6) + `DEFRA.BankHolidays.Tests` (MSTest, Moq)
**Target:** Azure Container Apps (ACA)
**Status:** PLAN ONLY - nothing in this document has been executed. No source files have been changed.
**Based on:** [BankHolidays_ACA_Detailed_Assessment.md](./BankHolidays_ACA_Detailed_Assessment.md) (AppCAT report `20261027070103`) and [ACA-Readiness-Assessment.md](./ACA-Readiness-Assessment.md).

## 1. Goal and scope

**Goal:** after this plan is executed, every P1 finding is fixed in code, the solution builds in Release with **0 warnings and 0 errors**, **all unit tests pass** (the 7 existing tests, updated where behaviour intentionally changes, plus new tests), and the application can be configured for Azure (Azure SQL with managed identity, Key Vault, Application Insights, Entra ID) without further code changes.

**In scope**

| Source | Items |
|---|---|
| AppCAT P1 (Potential) | `Connection.0001` (4), `Configuration.0003` (6), `Local.0006` (1) |
| Manual P1 | M-4 Application Insights, M-5 health endpoints, M-6 migrations, M-7 scheduled refresh (code side), M-8 consumer contract (documentation only), M-9 error detail |
| P0 items that are code and are tied to P1 work | M-3 SQL authentication (same fix as `Connection.0001`), M-2 unauthenticated write endpoints (Entra ID) |

**Explicitly deferred (per your instruction)**

- Dockerfile, `.dockerignore`, Azure Container Registry, ACA definition / IaC, CI/CD pipeline (M-1).
- The ACA scheduled **Job definition**, ingress/networking, and the actual deployment.
- P2 items (M-10 to M-15, AppCAT `Scale.0001`) except where a P1 change fixes them incidentally (see 3.6).
- Migrating EF6 to EF Core (considered and not selected; see decision D-1).

## 2. Issue-to-work-item traceability

| Finding | Priority | Work item | Result after execution |
|---|---|---|---|
| `Connection.0001` + M-3 (Integrated Security, on-prem hosts) | P1 / P0 | W2, W1 | Connection string supplied by environment/secret; managed-identity, SQL-auth and integrated modes selectable by configuration; no on-prem hosts in committed config |
| `Configuration.0003` (local config files) | P1 | W1 | Strongly typed options; env-var and Key Vault overrides; environment files hold no environment-specific hosts or secrets |
| `Local.0006` (gov.uk URL hard-coded) | P1 | W1, W5 | URL comes from configuration; fetched server-side only |
| M-4 Application Insights gate | P1 | W3 | Standard `APPLICATIONINSIGHTS_CONNECTION_STRING` and `ApplicationInsights:ConnectionString` both enable telemetry |
| M-5 No health endpoints | P1 | W4 | `/health/live` and `/health/ready` |
| M-6 Migrations in web process | P1 | W2, W6 | Startup migration guarded; scripted/CLI migration path for pipelines |
| M-7 Scheduled refresh (localhost script) | P1 | W5 | Server-side `Update` is job-friendly and authenticated; script parameterised; job definition deferred |
| M-8 Consumer URL | P1 | W8 | Contract and hand-off notes documented; consumer repo change tracked as a hand-off |
| M-9 Internal error text returned | P1 | W5 | Proper status codes and generic messages; details logged |
| M-2 Unauthenticated write endpoints | P0 | W7 | Entra ID JWT bearer + app role on write endpoints; read endpoints unchanged |

## 3. Decisions required before execution (defaults shown)

Execution proceeds on the defaults unless you say otherwise.

| ID | Decision | Default used in this plan | Alternative |
|---|---|---|---|
| D-1 | SQL authentication for Azure | **Managed identity via access token** on EF6 (`System.Data.SqlClient` 4.8.6 supports `SqlConnection.AccessToken`); SQL authentication and Integrated modes remain selectable by config | Migrate to EF Core + `Microsoft.Data.SqlClient` (`Authentication=Active Directory Default`): cleaner, but a larger change and out of this plan's scope |
| D-2 | Do SIT/UAT/Production on-prem IIS deployments still need the committed connection strings? | **No** - remove hosts from `appsettings.{SIT,UAT,Production}.json`; supply via environment variables / secrets | Keep them until the on-prem retirement date (then Azure and on-prem configs coexist; `Configuration.0003` stays open for those files) |
| D-3 | Entra scope for the browser UI | **API-only**: write endpoints require a bearer token with app role `BankHolidays.Refresh`; the UI refresh button is removed (refresh runs from the scheduled job or an authenticated API call) | Add interactive OIDC sign-in for the UI (about +1 day) |
| D-4 | Keep `POST /Home/Refresh` (client-supplied JSON)? | **Keep, but protected** by the same policy, returning generic errors | Remove it (smaller attack surface; the 2 related unit tests are replaced) |
| D-5 | Azure App Configuration | **Not adopted**; use environment variables, ACA secrets and the Key Vault configuration provider | Add the App Configuration provider (central/dynamic settings) |
| D-6 | Entra tenant / app registration for testing | Automated tests use a **test authentication handler**; real Entra sign-in validated in the later deployment phase | Provide a test tenant and app registration now to validate real tokens earlier |

## 4. Target configuration surface

All keys can be supplied as environment variables using `__` for `:` (for example `Database__AuthenticationMode`).

| Key | Purpose | Default (local dev) |
|---|---|---|
| `ConnectionStrings:HolidayContext` | SQL connection string (no credentials in `ManagedIdentity` mode) | LocalDB, in `appsettings.Development.json` only |
| `Database:AuthenticationMode` | `Integrated` \| `SqlPassword` \| `ManagedIdentity` | `Integrated` |
| `Database:ManagedIdentityClientId` | Optional user-assigned identity | empty |
| `ApplyMigrationsOnStartup` | Dev-only migrate on start | `false` |
| `BankHolidays:SourceUrl` | Upstream feed | `https://www.gov.uk/bank-holidays.json` |
| `BankHolidays:SourceTimeoutSeconds` | Upstream timeout | `30` |
| `ApplicationInsights:ConnectionString` or `APPLICATIONINSIGHTS_CONNECTION_STRING` | Telemetry | empty (telemetry off) |
| `KeyVault:Uri` | When set, Key Vault secrets are added as configuration (managed identity) | empty (provider not added) |
| `AzureAd:Instance`, `TenantId`, `ClientId`, `Audience` | Entra ID token validation | empty |
| `Authentication:AllowAnonymousWritesInDevelopment` | Dev-only bypass for write endpoints | `false` (ignored outside Development) |

## 5. Work items

Each item lists the change, files, tests, and its own done criteria. Every item must leave the solution building (Release, 0 warnings, 0 errors) with all tests passing before the next begins.

### W0 - Baseline (before any change)

1. Record baseline: `dotnet build -c Release` (expect 0 warnings / 0 errors), `dotnet test -c Release` (expect 7 passed), `dotnet list package --vulnerable --include-transitive` (expect none).
2. Record AppCAT baseline: 4 issues / 12 incidents / 22 story points (report `20261027070103`).
3. Work on a dedicated branch; one commit per work item so any item can be reverted independently.

### W1 - Configuration foundation (`Configuration.0003`, `Local.0006`, part of `Connection.0001`)

| Step | Change |
|---|---|
| 1.1 | Add options classes `DatabaseOptions`, `HolidaySourceOptions`; bind in `Program.cs` with validation on start (`ValidateOnStart`) |
| 1.2 | Add `appsettings.Development.json` holding the LocalDB connection string for local use only |
| 1.3 | Edit `appsettings.json`: remove the `Data Source=.` connection string (keep empty placeholder), add `Database`, `BankHolidays`, `KeyVault`, `AzureAd` sections with safe defaults |
| 1.4 | Edit `appsettings.SIT.json`, `.UAT.json`, `.Production.json`: remove the on-prem connection strings (per D-2); document the environment variables to set instead |
| 1.5 | Add optional Key Vault configuration provider: when `KeyVault:Uri` is set, add `Azure.Extensions.AspNetCore.Configuration.Secrets` with `DefaultAzureCredential`; when empty, add nothing |
| 1.6 | Move the gov.uk URL out of `HomeController` into `HolidaySourceOptions` (`BankHolidays:SourceUrl`); remove it from the view (see W5) |
| 1.7 | Add packages (`Azure.Identity`, `Azure.Extensions.AspNetCore.Configuration.Secrets`); use the latest stable versions compatible with `net10.0` and re-run the vulnerability check |

New tests: options validation (missing connection string fails fast; defaults applied), Key Vault gate (no provider added when `KeyVault:Uri` empty), `SourceUrl` binding.
Done when: no environment-specific host names remain in committed config; app starts locally using only `appsettings.Development.json`; startup fails with a clear message when no connection string is supplied outside Development.

### W2 - Azure SQL authentication (`Connection.0001`, M-3)

| Step | Change |
|---|---|
| 2.1 | Add `HolidayContext(DbConnection, bool contextOwnsConnection)` constructor; keep the existing string constructors (the test mock `new Mock<HolidayContext>("Data Source=unused")` keeps working) |
| 2.2 | Add `ISqlAccessTokenProvider` + `ManagedIdentityTokenProvider` (`DefaultAzureCredential`, scope `https://database.windows.net/.default`, optional `ManagedIdentityClientId`); register credential as a singleton so tokens are cached |
| 2.3 | Add `IHolidayContextFactory`: `Integrated` / `SqlPassword` build the context from the connection string as today; `ManagedIdentity` opens a `SqlConnection` with `AccessToken` set and passes it to the new constructor |
| 2.4 | Register `HolidayContext` as scoped through the factory (replacing `AddScoped(_ => new HolidayContext(connectionString))`) |
| 2.5 | Validate the connection string in `ManagedIdentity` mode with `SqlConnectionStringBuilder`: reject `Integrated Security`, `User ID`, `Password` and `Authentication`, which are incompatible with an access token; throw a clear startup error |
| 2.6 | Disable the EF6 database initializer at runtime (`Database.SetInitializer<HolidayContext>(null)`) so the app never creates or model-checks the database implicitly on Azure SQL |

New tests: factory selects the right mode; `ManagedIdentity` mode requests a token with the correct scope (fake provider); invalid connection-string combinations rejected; token provider not invoked in `Integrated`/`SqlPassword` modes.
Limitation: LocalDB cannot accept Entra access tokens, so the managed-identity path is verified here only with fakes and construction tests. Real verification needs Azure SQL and is part of the later deployment phase.
Done when: all three modes construct a working context in tests; local smoke test (Integrated against LocalDB) still returns the documented API responses.

### W3 - Application Insights (M-4)

| Step | Change |
|---|---|
| 3.1 | Replace the registration gate with a small helper `TelemetrySettings.ResolveConnectionString(IConfiguration)` that accepts `ApplicationInsights:ConnectionString` first, then `APPLICATIONINSIGHTS_CONNECTION_STRING` |
| 3.2 | Cloud role name: confirm the supported mechanism for Application Insights SDK 3.2.0 (OpenTelemetry-based) during execution - expected to be the standard `OTEL_SERVICE_NAME` / resource attributes - and document it; set in code only if the SDK offers a supported API |
| 3.3 | Exclude `/health/*` requests from telemetry **only if** SDK 3.2.0 exposes a supported filter; otherwise record as a follow-up rather than adding an unsupported workaround |

New tests: connection-string resolution (either key, precedence, both empty -> telemetry off).
Done when: setting either key enables registration and neither disables it (unit-tested); role-name and probe-filter decisions are written into the Operations notes (W8).

### W4 - Health endpoints (M-5)

| Step | Change |
|---|---|
| 4.1 | Register health checks: a `DatabaseHealthCheck` (opens a connection through the factory and runs `SELECT 1` with a short command timeout) tagged `ready` |
| 4.2 | Map `/health/live` (no checks - process only) and `/health/ready` (checks tagged `ready`); both anonymous; unaffected by the Entra policy (W7) |
| 4.3 | Measure first database request latency after a cold start; if material, add an EF model warm-up to the readiness path (recorded in the operations notes so ACA startup/readiness probe timeouts can be set later) |

New tests: `DatabaseHealthCheck` healthy/unhealthy via a fake probe; `WebApplicationFactory` test that `/health/live` returns 200 without a database and `/health/ready` returns 503 when the probe fails (add `Microsoft.AspNetCore.Mvc.Testing` to the test project; `Program` is already `public partial`).
Done when: both endpoints behave as above in tests and in a local smoke run.

### W5 - Refresh flow hardening (M-7 code side, M-9, `Local.0006`)

| Step | Change |
|---|---|
| 5.1 | `Home/Update`: fetch the feed server-side through a **named `HttpClient`** configured with `BankHolidays:SourceUrl` and `SourceTimeoutSeconds`; map failures to `502` (upstream) or `500` with a generic message; log the exception (add `ILogger<HomeController>`) |
| 5.2 | `Home/Refresh`: return `400` with a generic message for invalid data and `500` for save failures; **no exception text in responses**; log details |
| 5.3 | Keep the replace-all save in a single `SaveChanges` (already transactional); add a single-flight guard so two simultaneous refreshes in one instance do not overlap |
| 5.4 | View `Home\Index.cshtml`: per D-3, remove the browser-side gov.uk call and the Refresh button (it also removes the `if (response = "Ok")` assignment bug and the jQuery-1 `.success` call); keep the API/Swagger link; state that refresh is performed by the scheduled job or an authenticated API call |
| 5.5 | `Scripts\Powershell\DEFRA.BankHolidays.Update.ps1`: add `-BaseUrl` and optional `-AccessToken` parameters; keep the old URL as the default for on-prem use |
| 5.6 | Verify (do not assume) a suspected data-growth defect: `Update` removes `Holidays` rows only, and the schema suggests the child `EnglandWales`/`NorthernIreland`/`Scotland`/`Event` rows are not cascade-deleted. Check row counts across repeated refreshes on LocalDB; if confirmed, delete the orphaned children in the same transaction and add a regression test |

Existing tests that change **intentionally**:
- `HomeControllerTest.Refresh_returns_error_message_for_invalid_data` - now expects `400` with a generic message and no parser text.
- Both `HomeControllerTest` constructors gain the logger argument (and options if used).

New tests: `Update` success, upstream failure -> 502, upstream timeout, generic error bodies, single-flight guard, orphan-cleanup (if confirmed).
Done when: no response body contains exception text; the page no longer references gov.uk; repeated refreshes keep row counts stable (if 5.6 confirms the defect).

### W6 - Migrations (M-6)

| Step | Change |
|---|---|
| 6.1 | Refuse `ApplyMigrationsOnStartup=true` when `Database:AuthenticationMode=ManagedIdentity` with a clear error (EF6 `DbMigrator` cannot use an access token) and log a warning when it is used in a non-Development environment |
| 6.2 | Add command-line utilities handled **before** the web host starts: `--script <path>` writes the idempotent T-SQL for pending migrations (EF6 `MigratorScriptingDecorator`); `--migrate` applies migrations for `Integrated`/`SqlPassword` modes and exits with a non-zero code on failure |
| 6.3 | Generate and commit `Database\Migrations\001_InitialCreate.sql` using `--script`, so a pipeline or ACA Job can apply the schema to Azure SQL with Entra credentials without running the web app |
| 6.4 | Verify: apply the script to a scratch LocalDB and compare the resulting schema with the schema created by the EF6 migration (tables, columns, keys, indexes); confirm the app runs against the scripted database with the startup flag off |

New tests: argument parsing for `--script` / `--migrate`; guard in 6.1.
Done when: the scripted schema matches the migration-created schema and the app serves the documented API responses against it.

### W7 - Entra ID protection for write endpoints (M-2, P0 code item)

| Step | Change |
|---|---|
| 7.1 | Add `Microsoft.Identity.Web` and configure JWT bearer validation from the `AzureAd` section |
| 7.2 | Add policy `BankHolidays.Refresh` (requires the `BankHolidays.Refresh` app role); apply `[Authorize(Policy = ...)]` to `Home/Update` and `Home/Refresh` (and the migrate utilities need no HTTP surface) |
| 7.3 | **Read endpoints stay anonymous** (`GET api/values...`, Swagger, health) so the API contract and existing consumers are unaffected |
| 7.4 | Fail closed: outside Development, startup fails if `AzureAd` is not configured; in Development, writes are anonymous only when `Authentication:AllowAnonymousWritesInDevelopment=true` |
| 7.5 | Add a bearer security scheme to Swagger so an authenticated call can be made from the Swagger UI |
| 7.6 | Document the Entra objects the customer/ops team must create: app registration with app role `BankHolidays.Refresh`, assignment of the scheduled job's identity (and operators) to that role |

New tests (`WebApplicationFactory` with a test authentication handler, not real Entra): no token -> 401; token without the role -> 403; token with the role -> authorised (upstream client faked); read endpoints remain 200/204 anonymously; startup fails closed outside Development.
Limitation: validating real Entra-issued tokens requires a tenant and app registration (decision D-6); that happens in the deployment phase.
Done when: the write endpoints are unreachable without the role in tests and the existing API behaviour is unchanged for anonymous reads.

### W8 - Documentation and consumer hand-off (M-8)

| Step | Change |
|---|---|
| 8.1 | Add `docs\Operations-Notes.md`: configuration keys (section 4), environment variables, health endpoints, telemetry settings, migration commands and the SQL script, scheduled refresh call (with token), identity requirements |
| 8.2 | Add `docs\Consumer-Handoff.md`: unchanged API contract (query-string switching, PascalCase JSON, `204` when empty, legacy `api/Values/{id}` route), the new base URL placeholder, and the change required in `rpa-inspections-workbench` (`HolidayService:WorkingDaysUrl`, currently `http://d2vmbwa001/defra.bankholidays/api/Values/workingdays`). **The consumer repo is not modified by this plan** |
| 8.3 | Update `README.md` (currently the unmodified template) with build, test and local-run instructions |

## 6. Test plan

| Layer | Content | Count (indicative) |
|---|---|---|
| Existing unit tests | `ValuesControllerTest` (5, unchanged), `HomeControllerTest` (2, updated as described) | 7 |
| New unit tests | options validation, Key Vault gate, factory modes, token scope, connection-string validation, telemetry resolution, health check, error mapping, command parsing | about 20-25 |
| New pipeline tests (`WebApplicationFactory`) | `/health/live`, `/health/ready`, anonymous reads, JSON contract (PascalCase, `england-and-wales` key), `204` when empty, legacy `{id}` route, write endpoints 401/403/200, fail-closed startup | about 12-15 |
| Manual / scripted verification | LocalDB smoke test; scripted-vs-migrated schema comparison; repeated-refresh row counts | scripted, results recorded |

Test rules: no test calls Azure or the internet; the gov.uk client is faked; tests use synthetic data only.

## 7. Validation gates and definition of done

Run after **every** work item, and in full at the end:

1. `dotnet build DEFRA.BankHolidays.sln -c Release --no-incremental` -> **0 errors, 0 warnings**.
2. `dotnet test DEFRA.BankHolidays.sln -c Release` -> **all tests pass, exit code 0** (no skipped tests introduced).
3. `dotnet list package --vulnerable --include-transitive` -> none.
4. Local smoke test on LocalDB with synthetic data: documented API responses unchanged; health endpoints respond; write endpoints require authorisation.

**Final acceptance (all must hold):**

- Every P1 item in section 2 is implemented and traced to a test or a recorded verification.
- **AppCAT re-run** (`appcat analyze ... --target Any`) compared with the baseline of 12 incidents / 22 story points:
  - `Local.0006` incident for `HomeController.cs` is gone.
  - `Connection.0001` / `Configuration.0003` incidents for `appsettings.{SIT,UAT,Production}.json` are gone (per D-2).
  - Remaining incidents on `appsettings.json` are expected because a local configuration file still exists; they are accepted with the documented rationale (environment variables, ACA secrets, Key Vault provider) unless D-5 selects Azure App Configuration.
  - No new Mandatory incidents.
- `docs\Operations-Notes.md` and `docs\Consumer-Handoff.md` exist and match the implemented behaviour.
- No Dockerfile, IaC or pipeline files have been added (deferred).

## 8. Sequence and effort

| Order | Work item | Depends on | Effort (days) |
|---:|---|---|---:|
| 1 | W0 Baseline | - | 0.25 |
| 2 | W1 Configuration foundation | W0 | 1.0 |
| 3 | W2 Azure SQL authentication | W1 | 1.5 |
| 4 | W3 Application Insights | W1 | 0.5 |
| 5 | W4 Health endpoints | W2 | 0.75 |
| 6 | W6 Migrations | W2 | 1.0 |
| 7 | W5 Refresh flow hardening | W1 | 1.0 |
| 8 | W7 Entra ID protection | W4, W5 | 1.5 |
| 9 | W8 Documentation and hand-off | all | 0.5 |
| 10 | Final validation and AppCAT re-run | all | 0.5 |
| | **Total (indicative, +/- 40%)** | | **about 8.5** |

W3 can run in parallel with W2; W5 and W6 can run in parallel after W2.

## 9. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Managed identity cannot be verified locally (LocalDB does not accept tokens) | Unit tests with fakes; real verification against Azure SQL in the deployment phase; keep `SqlPassword` mode as a documented fallback |
| EF6 + `System.Data.SqlClient` is legacy; some Azure SQL options are unavailable | Documented under D-1; EF Core migration remains a separate, later option |
| Application Insights 3.2.0 may not expose a supported filter or role-name API | Verify during W3; fall back to standard OpenTelemetry environment variables and record follow-ups instead of unsupported workarounds |
| Removing the on-prem connection strings could break existing IIS deployments | Decision D-2; if on-prem must continue, keep those files and accept the remaining `Configuration.0003` / `Connection.0001` incidents |
| Removing the UI refresh button changes operator workflow | Decision D-3; refresh moves to the scheduled job / authenticated call, or add OIDC sign-in |
| Orphaned child rows may already exist in real databases | Step 5.6 verifies first; fix only if confirmed, and add a one-time cleanup note for operations |
| Consumer cannot call write endpoints without a token | Only write endpoints are protected; the consumer uses read endpoints, which stay anonymous |

## 10. Deferred to the container and deployment phase

Dockerfile and `.dockerignore`; ACR and ACA infrastructure as code; CI/CD pipeline; ACA scheduled Job definition (cron) that calls `Home/Update` with a managed-identity token; ingress, networking and probe settings (informed by W4's cold-start measurement); Azure SQL, Key Vault and Application Insights resource provisioning; Entra app registration and role assignments; deployed validation on ACA; consumer repo configuration change; P2 backlog (Swagger gating and `AllowedHosts`, client-library upgrade, forwarded headers, log levels, further test depth).

## 11. Execution checklist

- [ ] Confirm decisions D-1 to D-6 (or accept the defaults).
- [ ] W0 baseline recorded.
- [ ] W1, W2, W3, W4, W5, W6, W7, W8 completed, each passing the gates in section 7.
- [ ] Final build: 0 warnings, 0 errors.
- [ ] Final tests: all pass, exit code 0.
- [ ] Vulnerability check clean.
- [ ] Local smoke test and schema comparison recorded.
- [ ] AppCAT re-run compared with baseline.
- [ ] Operations notes and consumer hand-off written.
