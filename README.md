# DEFRA Bank Holidays

ASP.NET Core (.NET 10) service that serves UK bank holiday data and keeps it up to date from the configured holiday source. It stores data in SQL Server through Entity Framework 6.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server or LocalDB
- Docker (optional, for the container build)

## Project layout

| Path | Purpose |
|------|---------|
| `DEFRA.BankHolidays/DEFRA.BankHolidays` | The web application |
| `DEFRA.BankHolidays/DEFRA.BankHolidays.Tests` | MSTest unit and integration tests |
| `Database/` | Database scripts |
| `infra/` | Bicep for Azure Container Apps (see [infra/README.md](infra/README.md)) |
| `docs/` | Assessment, remediation plan and operations notes |

## Build and test

Run from this directory:

```powershell
dotnet build DEFRA.BankHolidays\DEFRA.BankHolidays.sln -c Release
dotnet test DEFRA.BankHolidays\DEFRA.BankHolidays.sln -c Release
```

To run the service locally against a test database, follow the [Local Testing Guide](docs/Local-Testing-Guide.md).

## Endpoints

| Method and path | Description |
|-----------------|-------------|
| `GET /api/values` | Bank holiday lookups (`date`, `start`, `end`, `includeFirstDate`) |
| `POST /Home/Update` | Refresh holidays from the source. Requires the `BankHolidays.Refresh` role |
| `GET /health/live` | Liveness probe |
| `GET /health/ready` | Readiness probe, including database connectivity |
| `GET /swagger` | API documentation |

## Containerisation

The multi-stage [Dockerfile](./Dockerfile) follows the Defra .NET container standard
([defra-docker-dotnetcore](https://github.com/DEFRA/defra-docker-dotnetcore)): a `development`
stage (`defradigital/dotnetcore-development`) restores, tests and publishes the app; a
`production` stage (`defradigital/dotnetcore`) runs it as the non-root `dotnet` user with write
permissions removed. The container listens on port 8080 by default (the `PORT` build argument).

Run from this directory (the `bank-holidays` folder is the build context):

```powershell
# Production image
docker build --target production -t defra-bankholidays -f Dockerfile .
docker run -p 8080:8080 -e ConnectionStrings__HolidayContext="<connection string>" defra-bankholidays

# Development image (dotnet watch, hot reload)
docker build --target development -t defra-bankholidays-dev -f Dockerfile .
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development -e ConnectionStrings__HolidayContext="<connection string>" defra-bankholidays-dev
```

The database schema is not created on start-up. Apply it with `DEFRA.BankHolidays.dll --migrate` (or `--script <file>`) using an identity that has DDL rights, as described in [infra/README.md](infra/README.md).

## Azure deployment

Infrastructure and deployment order are in [infra/README.md](infra/README.md). Operational and architecture background is in [docs/](docs/).
