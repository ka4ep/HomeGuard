# HomeGuard — Claude Code Context

HomeGuard is a **personal home equipment management PWA** for family use.
Tracks appliances, vehicles, warranties, service records, and maintenance schedules.
Deployed on a home Linux server. Design aesthetic: family-friendly, not corporate.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly (.NET 10), MudBlazor 9, PWA |
| Backend | ASP.NET Core Minimal API, Entity Framework Core, SQLite |
| Auth | FIDO2 / Passkeys (cookie-based, 401 on `/api/*`) |
| Deployment | Podman + podman-compose, home Linux server |
| Fonts | Plus Jakarta Sans — Regular (400), Medium (500), SemiBold (600) |
| Blob storage | NextCloud via WebDAV (planned) |
| Push | Web Push via VAPID (planned) |
| Calendar | iCal feed at `/api/calendar/feed.ics` |
| Notes | Tiptap rich text editor via JS interop (planned) |

---

## Project Structure

```
HomeGuard.sln
├── HomeGuard.Api/              — ASP.NET Core host + Minimal API endpoints
│   ├── Program.cs              — DI, middleware, endpoint registration
│   ├── Endpoints/              — *Endpoints.cs files, one per entity
│   └── wwwroot/                — Blazor WASM static files (published here)
├── HomeGuard.Application/      — Services, business logic
│   └── Services/               — *Service.cs
├── HomeGuard.Domain/           — Entities, enums, domain logic
│   ├── Entities/
│   └── Enums/
├── HomeGuard.Infrastructure/   — EF Core, repositories, SQLite
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   └── Repositories.cs     — all repos in one file
│   └── Migrations/
├── HomeGuard.Common/           — Shared DTOs, interfaces, constants
│   ├── DTOs/
│   └── Interfaces/
└── HomeGuard.Client/           — Blazor WASM app
    ├── Pages/
    ├── Services/               — ApiClients.cs, TimelineInterop.cs, etc.
    ├── Shared/                 — Layout, NavMenu
    ├── wwwroot/
    │   ├── css/
    │   │   ├── mud-overrides.css   ← single source of truth for colors
    │   │   └── timeline.css
    │   └── js/
    │       └── homeguard-timeline.js
    └── _Imports.razor
```

**Critical namespace rule:** The shared project is `HomeGuard.Common`.
`HomeGuard.Shared` does NOT exist — never use it.

---

## Architecture Patterns

### Backend

**Minimal API endpoint groups:**
```csharp
// ServiceRecordEndpoints.cs pattern
public static class ServiceRecordEndpoints
{
    public static void MapServiceRecordEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/service-records").RequireAuthorization();
        grp.MapGet("/",                          GetAll);
        grp.MapGet("/by-equipment/{equipId:guid}", GetByEquipment);
        grp.MapGet("/overdue",                   GetOverdue);
        grp.MapGet("/due-soon",                  GetDueSoon);
        grp.MapPost("/",                         Create);
        grp.MapPut("/{id:guid}",                 Update);
        grp.MapDelete("/{id:guid}",              Delete);
    }
    // private static async Task<IResult> handlers...
}
```

**SQLite concurrency:** Semaphore/queue pattern in repository base class — SQLite does not support concurrent writes.

**Auth:** Cookie auth. Unauthenticated requests to `/api/*` return 401 (not redirect). First registered user becomes owner. FIDO2 passkeys only — no passwords.

**DTOs:** All API responses use `*Dto` records in `HomeGuard.Common/DTOs/`. Entities never leave the domain layer.

### Frontend

**API clients** all live in `HomeGuard.Client/Services/ApiClients.cs`:
```csharp
public sealed class ServiceRecordApiClient(HttpClient _http)
{
    public Task<List<ServiceRecordDto>?> GetAllAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ServiceRecordDto>>("api/service-records", ct);
    // ...
}
```

**JS Interop:** `TimelineInterop.cs` wraps `homeguard-timeline.js`.
Timeline uses `vis-timeline 7.7.3` (CDN: `cdnjs.cloudflare.com/ajax/libs/vis-timeline/7.7.3/vis-timeline-graph2d.min.js`).

**MudBlazor gotchas:**
- `MudExpansionPanel` is unreliable in dynamically rebuilt trees → use `MudCollapse` + `HashSet<Guid>` toggle
- `AutoGrow` → `Sizing="InputSizing.Auto"` (MudBlazor 9)
- `MudAppBar` requires `Color="Color.Default"` to show correctly
- `MudChip` requires `T="string"` type parameter

**CSS architecture:**
- `mud-overrides.css` with `!important` is the **single source of truth** for colors
- `HomeGuardTheme.cs` PaletteLight settings are **ineffective** alongside `!important` overrides
- Pick one: either C# theme OR CSS overrides — currently using CSS overrides

**Component toggle pattern** (preferred over MudExpansionPanel):
```csharp
private HashSet<Guid> _expanded = [];
private void Toggle(Guid id) => _ = _expanded.Contains(id) ? _expanded.Remove(id) : _expanded.Add(id);
```

---

## Domain Models

### Equipment
Central entity. Has `MeterUnit` (e.g., "km", "mi", "m³", "kWh", "h") used by all its service records.

### ServiceRecord
```csharp
public class ServiceRecord
{
    public Guid          Id                    { get; set; }
    public Guid          EquipmentId           { get; set; }
    public string        Title                 { get; set; }
    public DateOnly      ServiceDate           { get; set; }
    public ServiceStatus Status                { get; set; }  // Completed | Planned
    public decimal?      MeterReading          { get; set; }  // replaces OdometerReading
    public string?       ServiceProvider       { get; set; }
    public decimal?      Cost                  { get; set; }
    public string?       Notes                 { get; set; }
    public Guid?         RecurringRuleId       { get; set; }
    public DateOnly?     OriginalPredictedDate { get; set; }  // audit trail for reschedules
    public ICollection<NotificationRule> NotificationRules { get; set; }
}

public enum ServiceStatus { Completed, Planned }
```

**Note:** `OdometerReading` (string) has been replaced by `MeterReading` (decimal?).
`NextServiceDate` has been removed — prediction is now handled by `RecurringRule`.

### RecurringRule
```csharp
public class RecurringRule
{
    public Guid     Id                    { get; set; }
    public Guid     EquipmentId           { get; set; }
    public string   Title                 { get; set; }   // canonical title = timeline row key
    public int?     IntervalDays          { get; set; }
    public decimal? IntervalMeter         { get; set; }
    public int      MaterializeDaysAhead  { get; set; } = 30
    public int      PredictionsAhead      { get; set; } = 2
    public bool     IsActive              { get; set; } = true
}
```

### Warranty
Has `StartDate`, `EndDate`, `EquipmentId`. Expired warranties are hidden by default in UI (toggle to show).

---

## Service Record Lifecycle (State Machine)

```
Predicted ──materialize──► Planned ──confirm──► Completed
(computed, no DB)            (in DB)              (in DB)
```

**Predicted:** Computed at runtime from `RecurringRule` + average of last N `Completed` records.
Not stored. Shown on timeline as gray dashed buttons. Multiple predictions shown ahead (`PredictionsAhead`).

**Planned:** Created automatically `MaterializeDaysAhead` days before predicted date.
Real DB record. Feeds iCal, triggers notifications. Date can be moved (reschedule).
`OriginalPredictedDate` stores what it was before any reschedule.

**Completed:** After actual service. User confirms date, adds `MeterReading`, cost, receipt, notes.
Computed average interval from `Completed` records to update next prediction.

---

## MeterReading / MeterUnit

`MeterUnit` is set on `Equipment`, not per record (set once, inherited). Examples:

| Equipment type | Unit | Value type |
|---|---|---|
| Car / motorcycle | km / mi | integer-ish (decimal ok) |
| Gas meter | m³ | decimal (e.g., 1234.567) |
| Electricity meter | kWh | decimal |
| Generator / tractor | h (hours) | decimal |
| Heat meter | GJ / MWh | decimal |

Use `decimal?` for `MeterReading` — covers all cases without loss.

---

## Key Conventions

- **Parallel API calls:** `await Task.WhenAll(taskA, taskB, taskC)` then `.Result`
- **HTML encode** user strings before embedding in timeline content/tooltip: `System.Net.WebUtility.HtmlEncode()`
- **iCal feed** at `/api/calendar/feed.ics` — no Google Calendar API needed
- **iOS Web Push** requires "Add to Home Screen" (iOS 16.4+)
- **Offline-first** is non-negotiable: IndexedDB outbox queue, idempotent operations, chunked resumable uploads — must be designed in, not retrofitted
- **Web Push notification timing** configurable per event: 6 months / 1 month / 1 day / day-of

---

## What's Done / Stable

- Full auth flow: FIDO2 passkeys, device management, first-user owner gate, last-credential protection
- Equipment CRUD
- Service records CRUD (all endpoints including `GET /api/service-records`)
- Warranty CRUD  
- Timeline page with vis-timeline (being replaced — see `docs/timeline-spec.md`)
- iCal feed endpoint
- Podman deployment (Containerfile, podman-compose.yml, .env.example)

## What's In Progress

- Custom timeline component (replacing vis-timeline — see `docs/timeline-spec.md`)
- `RecurringRule` entity and prediction engine (new)
- `MeterReading` migration from `OdometerReading` (string → decimal)

## What's Planned

- Offline support: IndexedDB outbox queue
- Web Push notifications
- Tiptap rich text editor for notes
- Receipt/document photo capture (`<input capture="environment">`)
- NextCloud blob storage via WebDAV
