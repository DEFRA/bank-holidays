# Consumer Hand-off - DEFRA Bank Holidays API

For teams that call the Bank Holidays service. **Read access is unchanged and anonymous.** Only the base address changes when the service moves.

## Contract (unchanged)

`GET {base}/api/values` selects its behaviour from the query string, exactly as the original Web API did:

| Request | Result |
|---|---|
| `GET /api/values` | All holidays as JSON; `204 No Content` when there is no data |
| `GET /api/values?date=2026-05-25` | `true` or `false`: is the date an England and Wales bank holiday |
| `GET /api/values?start=2026-05-22&end=2026-05-27[&includeFirstDate=true]` | Number of working days after `start` up to and including `end` (England and Wales) |

- JSON property names are as declared (not camelCase): `england-and-wales`, `Scotland`, `northern-ireland`, `Division`, `Events`, `Title`, `Date`, `Notes`, `Bunting`.
- The legacy route `GET /api/Values/{anything}?...` (for example `/api/Values/workingdays?start=...&end=...`) behaves the same as `/api/values`. Existing clients rely on this.
- Responses are served as JSON even when the client asks for `text/html`. XML responses are not supported.
- API documentation: `{base}/swagger` (`{base}/Help` redirects there).

## What consumers need to change

| Consumer | Setting | Currently | Change |
|---|---|---|---|
| `rpa-inspections-workbench` | `HolidayService:WorkingDaysUrl` (`appsettings.json`) | `http://d2vmbwa001/defra.bankholidays/api/Values/workingdays` | Set to the new service address, `https://<new-host>/api/Values/workingdays`, via configuration for each environment. **This repository does not change that application.** |

Notes:
- The new address will use `https`; ensure the consumer trusts the certificate chain.
- Because read endpoints stay anonymous, consumers do not need a token or any new credentials.
- Do not call `Home/Update` or `Home/Refresh` from consumers; they require the `BankHolidays.Refresh` app role and are used only by the scheduled refresh.

## Health and availability

`GET {base}/health/live` (process) and `GET {base}/health/ready` (database reachable, `503` when not) are available for monitoring.
