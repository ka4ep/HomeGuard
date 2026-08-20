# HomeGuard

Home equipment, warranty and service tracker.  
Blazor WASM PWA · ASP.NET Core · SQLite · Podman

---

## Getting started

### 1. Prerequisites

- .NET 10 SDK
- Node is NOT required — all JS is loaded from CDN or bundled as static files

### 2. VAPID keys (Web Push)

Generate once and put in `appsettings.Development.json` (never commit to git):

```bash
dotnet tool install -g webpush-keygen
webpush-keygen
```

```json
{
  "WebPush": {
    "VapidPublicKey":  "your_public_key",
    "VapidPrivateKey": "your_private_key",
    "VapidSubject":    "mailto:you@example.com"
  }
}
```

### 3. Database migration

```bash
# From solution root:
dotnet ef migrations add InitialCreate \
  --project src/HomeGuard.Infrastructure \
  --startup-project src/HomeGuard.Api

dotnet ef database update \
  --project src/HomeGuard.Infrastructure \
  --startup-project src/HomeGuard.Api
```

The database is created automatically on first run via `MigrateAsync()` in `Program.cs`,
so this step is optional for development.

### 4. Run (development)

```bash
cd src/HomeGuard.Api
dotnet run
# or, for hot reload:
dotnet watch
```

Open **`https://127.0.0.1:5500`** — not `localhost`. `HomeGuard.Api` hosts both the API
and the compiled Blazor WASM client in one process (the Client project is a
`ProjectReference`; its build output lands in `wwwroot`), so this is the only thing you
need to run. `appsettings.Development.json` pins `Fido2:ServerDomain` to `127.0.0.1`,
and browsers treat `localhost`/`127.0.0.1` as different WebAuthn RP IDs — passkey login
(and, until the origin is right, the API itself) silently breaks on the wrong host.

Accept the browser's self-signed-certificate warning; Kestrel's dev cert isn't trusted
by default, which is expected.

**Optional split setup** — the Client project's own dev server, for faster Blazor
iteration without restarting the Api process:
```bash
cd src/HomeGuard.Client
dotnet run   # http://127.0.0.1:5555; ApiBaseAddress already points at https://127.0.0.1:5500
```
Both origins are already in `Cors:Origins` for this reason. Not needed for normal work —
the single-process flow above is simpler and is what's actually exercised day to day.

### 5. Run with Podman (production)

```bash
cd infra
podman-compose up -d
```

The image is built from `infra/Containerfile`.  
Volumes: `homeguard-data` (SQLite), `homeguard-blobs` (local blob fallback).

### 6. Manual / LAN deployment (without Podman)

For testing on another machine on the LAN (a phone, a home server) without setting up a
full build toolchain there:

```bash
dotnet publish src/HomeGuard.Api -c Release -o ./publish
```

Copy `./publish` — **not** `bin/Debug/...`. A plain `dotnet build` never bundles the
compiled Blazor client into `wwwroot`; only `publish` does, so a raw Debug-build copy
serves nothing (blank page on `http`, 404 on every route over `https`).

In the copied `appsettings.json`, matching `.env.example`'s documented pattern:
```json
{
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:8080" } } },
  "Fido2": {
    "ServerDomain": "192.168.1.100",
    "Origins": ["http://192.168.1.100:8080"]
  }
}
```
`ServerDomain` is the bare host, `Origins` the full URL — both must exactly match what's
typed in the browser, or passkey login refuses the ceremony. And Kestrel must actually
bind to `0.0.0.0` (or the machine's LAN address) instead of its loopback-only default —
without an explicit `Kestrel:Endpoints` (or `ASPNETCORE_URLS`), nothing on the LAN can
reach the app regardless of firewall rules.

HTTPS redirection and HSTS only activate when an `Https` endpoint is actually configured
here — an HTTP-only LAN deployment stays HTTP-only instead of redirecting to a port
nothing is listening on.

---

## Project structure

```
src/
  HomeGuard.Domain/           Entity model, value objects, enums
  HomeGuard.Application/      Services, repository interfaces, sync protocol
  HomeGuard.Infrastructure/   EF Core, SQLite, Google Calendar, WebDAV, WebPush
  HomeGuard.Api/              ASP.NET Core Minimal API, background services
  HomeGuard.Diagnostics/      Api server startup diagnostics code
  HomeGuard.Client/           Blazor WASM PWA
  HomeGuard.Common/           DTOs shared between Api and Client (formerly Common)
tests/
  HomeGuard.Tests.Unit/
  HomeGuard.Tests.Integration/
infra/
  Containerfile
  podman-compose.yml
```

## iCal feed

Family Wall and NextCloud can subscribe to:
```
http://your-server:8080/api/calendar/feed.ics
```
The feed is refreshed every 6 hours and contains all active warranties and upcoming service dates.

## License

MIT
