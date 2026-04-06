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

Open two terminals:

```bash
# Terminal 1 — API
cd src/HomeGuard.Api
dotnet run

# Terminal 2 — Blazor client
cd src/HomeGuard.Client
dotnet run
```

The client runs on `http://localhost:5010` by default.  
`appsettings.Development.json` in the Client project points `ApiBaseAddress` at the Api.

### 5. Run with Podman (production)

```bash
cd infra
podman-compose up -d
```

The image is built from `infra/Containerfile`.  
Volumes: `homeguard-data` (SQLite), `homeguard-blobs` (local blob fallback).

---

## Project structure

```
src/
  HomeGuard.Domain/           Entity model, value objects, enums
  HomeGuard.Application/      Services, repository interfaces, sync protocol
  HomeGuard.Infrastructure/   EF Core, SQLite, Google Calendar, WebDAV, WebPush
  HomeGuard.Api/              ASP.NET Core Minimal API, background services
  HomeGuard.Client/           Blazor WASM PWA
  HomeGuard.Shared/           DTOs shared between Api and Client (formerly Common)
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
