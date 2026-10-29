# Local Testing Guide - DEFRA Bank Holidays (dummy data)

How to run the service on your machine against a local SQL Server (LocalDB) with **dummy data** and confirm that the API still behaves as before. Nothing here needs Azure, an Entra ID tenant or internet access.

**Time needed:** about 10 minutes. **Everything runs in PowerShell from the repository folder** (`bank-holidays`).

## Prerequisites

| Need | Check |
|---|---|
| .NET 10 SDK | `dotnet --version` |
| SQL Server LocalDB | `sqllocaldb info` (should list `MSSQLLocalDB`) |
| `sqlcmd` (used to look at the database; optional but recommended) | `sqlcmd -?` |
| A free local port (this guide uses `5000`) | `Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue` returns nothing |

## 1. Run the automated tests first

These need no database, Azure or internet access. They already exercise the real routing, JSON formatting, authorization and health checks.

```powershell
dotnet test DEFRA.BankHolidays\DEFRA.BankHolidays.sln -c Release
```

Expected: `Passed! - Failed: 0, Passed: 68, Skipped: 0, Total: 68`.

## 2. Prepare an isolated local database

This guide uses its own database called `BhLocalTest`, so your other databases are untouched. Run these in the window that will host the app (**window 1**).

```powershell
sqllocaldb start MSSQLLocalDB

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS        = 'http://127.0.0.1:5000'
$env:ConnectionStrings__HolidayContext = 'Data Source=(localdb)\MSSQLLocalDB; Initial Catalog=BhLocalTest; Integrated Security=True; MultipleActiveResultSets=True; Encrypt=False'

# creates the BhLocalTest database and applies the schema
dotnet run -c Release --no-launch-profile --project DEFRA.BankHolidays\DEFRA.BankHolidays -- --migrate
```

Expected last line: `Migrations applied.`

Why these options:

- `-c Release`: builds separately from the Debug build, so it still works if an IDE debug session is running and has the Debug files locked.
- `--no-launch-profile`: ignores `Properties\launchSettings.json`. Without it, `dotnet run` uses the ports and environment from that file and **overrides `ASPNETCORE_URLS`** (it also fails with "address already in use" if your IDE instance is already running on those ports).
- `Encrypt=False` is for LocalDB only. The service uses the Microsoft.Data.SqlClient provider, which encrypts by default.

## 3. Start the service

Still in **window 1**:

```powershell
dotnet run -c Release --no-launch-profile --project DEFRA.BankHolidays\DEFRA.BankHolidays
```

Leave it running. Wait for `Now listening on: http://127.0.0.1:5000`.

> **Already started the app from your IDE?** Use that instance instead: it listens on the ports in `Properties\launchSettings.json` (currently `http://localhost:53459`). It uses the connection string from `appsettings.Development.json` (database `DEFRA.BankHolidays` on LocalDB), so run `dotnet run -c Release --no-launch-profile --project DEFRA.BankHolidays\DEFRA.BankHolidays -- --migrate` against that database first (set `ASPNETCORE_ENVIRONMENT=Development` and do not set `ConnectionStrings__HolidayContext`). Then replace `http://127.0.0.1:5000` and `BhLocalTest` in the steps below.

## 4. Check health and the empty state (window 2)

Open a **second PowerShell window** in the repository folder.

```powershell
$b = 'http://127.0.0.1:5000'
(Invoke-WebRequest "$b/health/live"  -UseBasicParsing).StatusCode    # 200
(Invoke-WebRequest "$b/health/ready" -UseBasicParsing).StatusCode    # 200 (database reachable)
(Invoke-WebRequest "$b/api/values"   -UseBasicParsing).StatusCode    # 204 (no data yet)
```

## 5. Load dummy data

Writes are allowed without a token **in Development only**, so no sign-in is needed here. This dataset is invented; no real data is used.

```powershell
$json = '{"england-and-wales":{"division":"england-and-wales","events":[{"title":"Spring bank holiday","date":"2026-05-25","notes":"","bunting":true},{"title":"Summer bank holiday","date":"2026-08-31","notes":"","bunting":true},{"title":"Christmas Day","date":"2026-12-25","notes":"","bunting":true}]},"scotland":{"division":"scotland","events":[{"title":"Summer bank holiday","date":"2026-08-03","notes":"","bunting":false}]},"northern-ireland":{"division":"northern-ireland","events":[{"title":"Battle of the Boyne","date":"2026-07-13","notes":"","bunting":false}]}}'

Invoke-WebRequest "$b/Home/Refresh" -Method Post -Body ("data=" + [uri]::EscapeDataString($json)) -ContentType 'application/x-www-form-urlencoded' -UseBasicParsing
```

Expected: status `200`, content `Ok`.

To load the **real** gov.uk feed instead, call `Invoke-WebRequest "$b/Home/Update" -Method Post -UseBasicParsing`. That needs outbound internet access to `https://www.gov.uk/bank-holidays.json`; if it is blocked you get `502`.

## 6. Check that the API behaves as before

| Request | Expected |
|---|---|
| `GET /api/values` | `200`, JSON with keys `england-and-wales`, `Scotland`, `northern-ireland` (PascalCase, not camelCase); each event has `Title`, `Date`, `Notes`, `Bunting`; 3 / 1 / 1 events |
| `GET /api/values?date=2026-05-25` | `true` |
| `GET /api/values?date=2026-05-26` | `false` |
| `GET /api/values?date=2026-12-25` | `true` |
| `GET /api/values?start=2026-05-22&end=2026-05-27` | `2` (Mon 25 May is a holiday; the weekend is skipped) |
| `GET /api/values?start=2026-05-22&end=2026-05-27&includeFirstDate=true` | `3` |
| `GET /api/values?start=2026-12-21&end=2026-12-29` | `5` |
| `GET /api/Values/workingdays?start=2026-05-22&end=2026-05-27` | `2` (the legacy route that `rpa-inspections-workbench` uses) |
| `GET /swagger`, `GET /Help` | Swagger UI (`/Help` redirects to it) |
| `GET /health/ready` | `200` |

### Run all the checks at once

This script clears the test database tables, runs every check above plus the refresh-stability and security checks, and prints `PASS` or `FAIL` for each. It can be run repeatedly.

```powershell
$b = 'http://127.0.0.1:5000'; $srv = '(localdb)\MSSQLLocalDB'; $db = 'BhLocalTest'; $global:fail = 0
function Check($name, $actual, $expected) {
    if ("$actual".Trim() -eq "$expected") { "PASS  $name" } else { "FAIL  $name (expected '$expected', got '$actual')"; $global:fail++ }
}
function Status($url, $body = $null) {
    try {
        $p = @{ Uri = "$b$url"; UseBasicParsing = $true }
        if ($body) { $p.Method = 'Post'; $p.Body = $body; $p.ContentType = 'application/x-www-form-urlencoded' }
        [int](Invoke-WebRequest @p).StatusCode
    } catch { if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'ERR' } }
}
function Body($url) { (Invoke-WebRequest "$b$url" -UseBasicParsing).Content.Trim() }
$counts = "SET NOCOUNT ON; SELECT 'Holidays='+CAST((SELECT COUNT(*) FROM dbo.Holidays) AS varchar)+' EnglandWales='+CAST((SELECT COUNT(*) FROM dbo.EnglandWales) AS varchar)+' Scotland='+CAST((SELECT COUNT(*) FROM dbo.Scotland) AS varchar)+' NorthernIreland='+CAST((SELECT COUNT(*) FROM dbo.NorthernIreland) AS varchar)+' Event='+CAST((SELECT COUNT(*) FROM dbo.Event) AS varchar)"

# start from an empty database
sqlcmd -S $srv -E -d $db -Q "DELETE FROM dbo.Event; DELETE FROM dbo.Holidays; DELETE FROM dbo.EnglandWales; DELETE FROM dbo.Scotland; DELETE FROM dbo.NorthernIreland;" | Out-Null

Check 'health live'                       (Status '/health/live') 200
Check 'health ready (database reachable)' (Status '/health/ready') 200
Check 'empty database -> 204'             (Status '/api/values') 204

$json = '{"england-and-wales":{"division":"england-and-wales","events":[{"title":"Spring bank holiday","date":"2026-05-25","notes":"","bunting":true},{"title":"Summer bank holiday","date":"2026-08-31","notes":"","bunting":true},{"title":"Christmas Day","date":"2026-12-25","notes":"","bunting":true}]},"scotland":{"division":"scotland","events":[{"title":"Summer bank holiday","date":"2026-08-03","notes":"","bunting":false}]},"northern-ireland":{"division":"northern-ireland","events":[{"title":"Battle of the Boyne","date":"2026-07-13","notes":"","bunting":false}]}}'
$form = 'data=' + [uri]::EscapeDataString($json)
Check 'load dummy data'                   (Status '/Home/Refresh' $form) 200

$all = Body '/api/values' | ConvertFrom-Json
Check 'JSON key names (not camelCase)'    ($all.PSObject.Properties.Name -join ',') 'england-and-wales,Scotland,northern-ireland'
Check 'event field names'                 ($all.'england-and-wales'.Events[0].PSObject.Properties.Name -join ',') 'Title,Date,Notes,Bunting'
Check 'event counts (EW/Scotland/NI)'     ("$($all.'england-and-wales'.Events.Count)/$($all.Scotland.Events.Count)/$($all.'northern-ireland'.Events.Count)") '3/1/1'
Check 'date 2026-05-25 is a holiday'      (Body '/api/values?date=2026-05-25') 'true'
Check 'date 2026-05-26 is not'            (Body '/api/values?date=2026-05-26') 'false'
Check 'date 2026-12-25 is a holiday'      (Body '/api/values?date=2026-12-25') 'true'
Check 'working days 22 -> 27 May'         (Body '/api/values?start=2026-05-22&end=2026-05-27') 2
Check 'working days + includeFirstDate'   (Body '/api/values?start=2026-05-22&end=2026-05-27&includeFirstDate=true') 3
Check 'working days 21 -> 29 Dec'         (Body '/api/values?start=2026-12-21&end=2026-12-29') 5
Check 'legacy workingdays route'          (Body '/api/Values/workingdays?start=2026-05-22&end=2026-05-27') 2
Check 'swagger document'                  (Status '/swagger/v1/swagger.json') 200
Check 'invalid refresh data -> 400'       (Status '/Home/Refresh' 'data=garbage') 400

# the refresh must replace the data, not accumulate rows
foreach ($n in 1..3) { $null = Status '/Home/Refresh' $form }
Check 'row counts stable after 4 refreshes' (sqlcmd -S $srv -E -d $db -h -1 -W -Q $counts) 'Holidays=1 EnglandWales=1 Scotland=1 NorthernIreland=1 Event=5'

if ($global:fail -eq 0) { 'ALL CHECKS PASSED' } else { "$global:fail CHECK(S) FAILED" }
```

Expected last line: `ALL CHECKS PASSED`.

## 7. Check the security behaviour

**Fail-closed startup.** Outside Development the app refuses to start unless Entra ID is configured. Stop the app in window 1 (Ctrl+C), then:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet run -c Release --no-launch-profile --project DEFRA.BankHolidays\DEFRA.BankHolidays
```

Expected: it exits with `Microsoft Entra ID is not configured. Set 'AzureAd:TenantId' and 'AzureAd:ClientId'...`. Set `$env:ASPNETCORE_ENVIRONMENT = 'Development'` again before continuing.

**Write endpoints with and without a token.** Real Entra ID token validation cannot be tried locally without a tenant. The 401 (no token), 403 (token without the `BankHolidays.Refresh` role) and 200 (with the role) behaviour is covered by the automated tests in step 1 (`WriteEndpointAuthorizationTest`), which stand in for the token validation step.

## 8. Compare with the original behaviour (optional)

A copy of the source before the remediation was kept at `C:\Users\v-ramasua\.copilot\session-state\4cdfe8e3-4d7d-49a9-9457-b5df6d64eeb5\files\bank-holidays-baseline`. To compare, build and run that copy against a different empty database and port, load the same dummy data (the original `Home/Refresh` returns `Ok` with status 200), and compare the responses in step 6. Expected differences:

| Behaviour | Original | Now |
|---|---|---|
| Anonymous `POST Home/Refresh` / `Home/Update` | Allowed in every environment | Needs the `BankHolidays.Refresh` role; anonymous only in Development |
| Invalid refresh data | `200` with the parser's error text | `400` with a generic message |
| Home page | Has a Refresh button | No button (refresh runs as a scheduled, authenticated call) |
| Refresh | Left old child rows behind | Replaces all rows |
| Read API (`GET api/values...`) | - | **Same responses** |

## 9. Clean up

Stop the app (Ctrl+C in window 1), clear the variables, and drop the test database:

```powershell
Remove-Item Env:\ASPNETCORE_ENVIRONMENT, Env:\ASPNETCORE_URLS, Env:\ConnectionStrings__HolidayContext -ErrorAction SilentlyContinue
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "ALTER DATABASE [BhLocalTest] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [BhLocalTest]"
```

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `Failed to bind to address ... address already in use` | Another instance (often one started from your IDE) already uses the port. `dotnet run` without `--no-launch-profile` also forces the ports from `launchSettings.json`. Add `--no-launch-profile`, choose a free port in `ASPNETCORE_URLS`, or stop the other process. |
| Build error `MSB3021 ... DEFRA.BankHolidays.exe ... being used by another process` | A running instance locks the Debug build. Use `-c Release` as in this guide, or stop the running process. |
| `Connection string 'HolidayContext' is not configured` | `ASPNETCORE_ENVIRONMENT` is not `Development` and `ConnectionStrings__HolidayContext` is not set. Set one of them. |
| `Cannot open database "BhLocalTest"` or tables missing (`Invalid object name 'dbo.Holidays'`) | The schema has not been created. Run the `--migrate` command in step 2. The app never creates the schema itself. |
| `A connection was successfully established ... certificate chain was issued by an authority that is not trusted` | The connection string is missing `Encrypt=False` (LocalDB) or `TrustServerCertificate=True` (other local servers). |
| `GET /health/ready` returns `503` | The database cannot be reached: check `sqllocaldb info MSSQLLocalDB` shows `Running` (`sqllocaldb start MSSQLLocalDB`) and the connection string. |
| `POST /Home/Update` returns `502` | The gov.uk feed cannot be reached or returned invalid data. Use `Home/Refresh` with the dummy JSON instead. |
| `POST` to a write endpoint returns `401` | The app is not running in Development, or `Authentication:AllowAnonymousWritesInDevelopment` is `false`. Check `$env:ASPNETCORE_ENVIRONMENT`. |

See also: [Operations notes](./Operations-Notes.md) for all configuration settings and [README](../README.md) for a short quick start.
