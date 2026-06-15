# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ASP.NET Core 6.0 web application that delivers configurable on-page notifications (popup / inline HTML / link / modal) to external websites. The server stores notification configs and per-user view/click state in Couchbase, exposes a JSON API consumed by a JavaScript client (`wwwroot/js/notification-client*.js`) embedded by partner sites, and ships an MVC admin area for operators.

The solution is single-project: `NotificationAPI.sln` -> `NotificationAPI/NotificationAPI.csproj`. There are no tests.

## Build & Run

```bash
# From the repo root (D:\Xmedia\2025\NotificationAPI)
dotnet restore NotificationAPI.sln
dotnet build   NotificationAPI.sln -c Debug
dotnet run     --project NotificationAPI/NotificationAPI.csproj

# Default dev URLs (see NotificationAPI/Properties/launchSettings.json):
#   https://localhost:7227
#   http://localhost:5201
# Swagger UI is enabled only in Development at /swagger.
```

The csproj references two private DLLs by relative `HintPath` outside this repo:

```
..\..\..\2024\ATS_New\ATS1Solution\DLL\XMUtility.dll
..\..\..\2024\ATS_New\ATS1Solution\DLL\XUtil.dll
```

Both must exist at that relative location for the build to succeed. `XMUtility.XUtility.UnixTime(...)` is used in model defaults (e.g. `Models/NotificationConfig.cs`), so the references are load-bearing - do not "clean them up".

## External Dependencies

- **Couchbase Server** at `http://x2cache.com:8091`, bucket `xlog`, key prefix `nof_` (see `appsettings.json` -> `Couchbase`). The app uses **CouchbaseNetClient 2.7.8** (legacy 2.x API: `Cluster` / `IBucket` / `OpenBucket` / `PasswordAuthenticator`), not the 3.x SDK - keep that in mind when editing `Services/Couchbase/*`. A successful build is meaningless without Couchbase reachable; many endpoints will throw on first request otherwise.
- **API keys** for `POST /api/Notification/external[/batch]` are a flat list in `appsettings.json` -> `ApiKeys` and validated by `Services/ApiKeyValidator.cs`.
- **User store for admin login** is a plain JSON file at `NotificationAPI/Data/users.json`, auto-created with an `admin / admin123` account on first run (`Services/UserService.cs`). Passwords are stored in clear text in that file - this is by design of the current code, not a bug to "fix" opportunistically.

## Architecture

### Two parallel surfaces over one data layer
- **Public JSON API** - `Controllers/NotificationController.cs`, route prefix `/api/Notification`. Consumed by the JS client and by external integrators. The two write endpoints are `POST /api/Notification/external` and `POST /api/Notification/external/batch`; both require `apiKey` inside the JSON body (not a header). Full field reference lives in `huong-dan-ky-thuat-api.md` and `ExternalSystemIntegrationGuide.md` at the repo root - prefer updating those docs over inventing new ones when the contract changes.
- **Admin MVC area** - `Areas/Admin/**`. `AdminBaseController` enforces `[Authorize(Roles = "Admin")]` with cookie auth (`AccountController` + `Views/Account`). Area routing is wired through `Areas/Admin/AdminAreaRegistration.cs` (`AdminAreaConvention : IControllerModelConvention`) which is registered as an MVC convention in `Program.cs`; controllers under the `*.Areas.Admin` namespace automatically get `area = "Admin"`. The area route is mapped **before** the default route in `Program.cs` - keep that order.

Both surfaces talk to the same `INotificationCB` (`Services/Couchbase/NotificationCB.cs`), which is the single Couchbase data-access seam. The `*VM` / `Paging*` types in `Models/` are the shapes returned to admin views and the public API respectively.

### Couchbase connection lifecycle
`CouchbaseConnectionManager` is a hand-rolled lazy singleton (double-checked lock, static `_instance`). `NotificationCB` is registered as a singleton in `Extensions/ServiceCollectionExtensions.cs::AddCacheConfiguration` and obtains its `IBucket` from the manager. `CouchbaseCleanupService` is an `IHostedService` whose `StopAsync` closes the bucket on shutdown. When changing connection code, preserve the singleton invariant - opening additional buckets per-request will exhaust connections against the live cluster.

### Logging
Serilog is the only logger. `Program.cs` overrides `Microsoft` and `System` categories to `Fatal` (i.e. effectively silenced) and writes daily-rolling files to `NotificationAPI/logs/notification-api-*.log`. **Note:** `appsettings.json` still has a `Logging:LogLevel` section, but Serilog ignores it - configure verbosity in `Program.cs`. `LoggingConfiguration.md` describes an older Microsoft.Extensions.Logging setup that no longer matches the code; treat it as historical context only.

### Domain enums to know
`Enums/ShowType.cs` and `Enums/Status.cs` define numeric values used across DTOs, models, and the JS client (e.g. `ShowTypeNotification`: 1=Popup, 2=HTML, 3=Link, 4=Modal; `DeviceType`: 1=All, 2=Website, 3=MobileWeb, 4=Mobile). `huong-dan-ky-thuat-api.md` is the canonical reference - if you change a numeric value, update both the C# enum and that doc, and search `wwwroot/js/notification-client*.js` for hardcoded numbers.

### JavaScript client
`wwwroot/js/notification-client.js` (plus `-v1`/`-v2`) is the embeddable client. It is served as a static file and is the consumer of the public API - changing API response shapes generally requires a matching edit there. `wwwroot/debug.html` is a manual smoke-test harness.

## Conventions

- **Language**: comments, log messages, and user-facing strings are in Vietnamese throughout. Match the existing language when editing nearby code.
- **JSON shape**: models use Newtonsoft `[JsonProperty("snake_case")]` to map .NET PascalCase to snake_case on the wire. Add the attribute when introducing new fields, otherwise the JS client will not see them.
- **API key transport**: keys travel in the request *body*, not headers. Do not "modernize" this to `X-Api-Key` without coordinating with integrators - the external integration guides document the body form.
- **Couchbase keys**: always go through `NotificationCB.CreateKey(id)` which prepends `_preKey` (`nof_`). Never hand-construct keys.
- **CORS**: `Program.cs` registers an `AllowAll` policy and applies it globally. This is intentional because the JS client runs on third-party domains.
