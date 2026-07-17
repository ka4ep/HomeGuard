# Graph Report - HomeGuard  (2026-07-17)

## Corpus Check
- 141 files · ~48,736 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1696 nodes · 2793 edges · 103 communities (89 shown, 14 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 25 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `3fd92158`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Warranty & Calendar|Warranty & Calendar]]
- [[_COMMUNITY_Blob & Notifications|Blob & Notifications]]
- [[_COMMUNITY_Domain & Repos|Domain & Repos]]
- [[_COMMUNITY_Equipment Module|Equipment Module]]
- [[_COMMUNITY_Service Records|Service Records]]
- [[_COMMUNITY_Client API Clients|Client API Clients]]
- [[_COMMUNITY_Equipment Detail UI|Equipment Detail UI]]
- [[_COMMUNITY_Infrastructure Repos|Infrastructure Repos]]
- [[_COMMUNITY_Tech Stack & Brand|Tech Stack & Brand]]
- [[_COMMUNITY_Service Records UI|Service Records UI]]
- [[_COMMUNITY_Warranties UI|Warranties UI]]
- [[_COMMUNITY_Settings UI|Settings UI]]
- [[_COMMUNITY_Auth Endpoints|Auth Endpoints]]
- [[_COMMUNITY_Home Dashboard|Home Dashboard]]
- [[_COMMUNITY_Startup Diagnostics|Startup Diagnostics]]
- [[_COMMUNITY_Equipment List UI|Equipment List UI]]
- [[_COMMUNITY_Auth Client|Auth Client]]
- [[_COMMUNITY_Client DB & Outbox|Client DB & Outbox]]
- [[_COMMUNITY_NuGet Dependencies|NuGet Dependencies]]
- [[_COMMUNITY_EF Core & UnitOfWork|EF Core & UnitOfWork]]
- [[_COMMUNITY_Integration Tests|Integration Tests]]
- [[_COMMUNITY_Login UI|Login UI]]
- [[_COMMUNITY_Background Services|Background Services]]
- [[_COMMUNITY_Timeline Interop|Timeline Interop]]
- [[_COMMUNITY_Timeline UI|Timeline UI]]
- [[_COMMUNITY_Sync Processor|Sync Processor]]
- [[_COMMUNITY_Blob Storage Service|Blob Storage Service]]
- [[_COMMUNITY_Document Capture|Document Capture]]
- [[_COMMUNITY_Notification Scheduler|Notification Scheduler]]
- [[_COMMUNITY_API Dependencies|API Dependencies]]
- [[_COMMUNITY_App Root & Router|App Root & Router]]
- [[_COMMUNITY_Config Masker|Config Masker]]
- [[_COMMUNITY_Shared Projects|Shared Projects]]
- [[_COMMUNITY_Startup Validator|Startup Validator]]
- [[_COMMUNITY_Main Layout|Main Layout]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_EF Migrations|EF Migrations]]
- [[_COMMUNITY_Nav Menu|Nav Menu]]
- [[_COMMUNITY_Launch Settings|Launch Settings]]
- [[_COMMUNITY_Client DB JS|Client DB JS]]
- [[_COMMUNITY_PWA Manifest|PWA Manifest]]
- [[_COMMUNITY_Document Capture JS|Document Capture JS]]
- [[_COMMUNITY_API Auth Handler|API Auth Handler]]
- [[_COMMUNITY_Equipment Form Page|Equipment Form Page]]
- [[_COMMUNITY_Equipment Form Shared|Equipment Form Shared]]
- [[_COMMUNITY_Service Worker|Service Worker]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_DateRange Value|DateRange Value]]
- [[_COMMUNITY_Model Snapshot|Model Snapshot]]
- [[_COMMUNITY_Client Dependencies|Client Dependencies]]
- [[_COMMUNITY_Auth JS (WebAuthn)|Auth JS (WebAuthn)]]
- [[_COMMUNITY_Timeline JS|Timeline JS]]
- [[_COMMUNITY_DB Startup Extensions|DB Startup Extensions]]
- [[_COMMUNITY_Passkey Auth Setup|Passkey Auth Setup]]
- [[_COMMUNITY_Initial Migration|Initial Migration]]
- [[_COMMUNITY_Continuation Migration|Continuation Migration]]
- [[_COMMUNITY_App Service Extensions|App Service Extensions]]
- [[_COMMUNITY_Client Service Extensions|Client Service Extensions]]
- [[_COMMUNITY_Form Models|Form Models]]
- [[_COMMUNITY_Push JS (VAPID)|Push JS (VAPID)]]
- [[_COMMUNITY_Tag Value|Tag Value]]
- [[_COMMUNITY_Validation Result|Validation Result]]
- [[_COMMUNITY_Captured Document|Captured Document]]
- [[_COMMUNITY_Community 71|Community 71]]
- [[_COMMUNITY_Community 72|Community 72]]
- [[_COMMUNITY_Community 73|Community 73]]
- [[_COMMUNITY_Community 74|Community 74]]
- [[_COMMUNITY_Community 75|Community 75]]
- [[_COMMUNITY_Community 76|Community 76]]
- [[_COMMUNITY_Community 77|Community 77]]
- [[_COMMUNITY_Community 78|Community 78]]
- [[_COMMUNITY_Community 79|Community 79]]
- [[_COMMUNITY_Community 80|Community 80]]
- [[_COMMUNITY_Community 81|Community 81]]
- [[_COMMUNITY_Community 82|Community 82]]
- [[_COMMUNITY_Community 83|Community 83]]
- [[_COMMUNITY_Community 84|Community 84]]
- [[_COMMUNITY_Community 85|Community 85]]
- [[_COMMUNITY_Community 86|Community 86]]
- [[_COMMUNITY_Community 87|Community 87]]
- [[_COMMUNITY_Community 88|Community 88]]
- [[_COMMUNITY_Community 89|Community 89]]
- [[_COMMUNITY_Community 90|Community 90]]
- [[_COMMUNITY_Community 91|Community 91]]
- [[_COMMUNITY_Community 92|Community 92]]
- [[_COMMUNITY_Community 93|Community 93]]
- [[_COMMUNITY_Community 94|Community 94]]
- [[_COMMUNITY_Community 95|Community 95]]
- [[_COMMUNITY_Community 96|Community 96]]
- [[_COMMUNITY_Community 97|Community 97]]
- [[_COMMUNITY_Community 98|Community 98]]
- [[_COMMUNITY_Community 99|Community 99]]
- [[_COMMUNITY_Community 100|Community 100]]
- [[_COMMUNITY_Community 101|Community 101]]
- [[_COMMUNITY_Community 102|Community 102]]

## God Nodes (most connected - your core abstractions)
1. `ServiceRecord` - 35 edges
2. `Warranty` - 29 edges
3. `Equipment` - 27 edges
4. `RecurringRuleService` - 24 edges
5. `ServiceRecordService` - 21 edges
6. `WarrantyService` - 20 edges
7. `StartupDiagnostics` - 20 edges
8. `BlobEntry` - 19 edges
9. `RecurringRule` - 19 edges
10. `EquipmentService` - 17 edges

## Surprising Connections (you probably didn't know these)
- `CI Workflow` --references--> `Container Configuration`  [INFERRED]
  .github/workflows/build.yml → infra/podman-compose.yml
- `Container Configuration` --references--> `VAPID Keys`  [EXTRACTED]
  infra/podman-compose.yml → README.md
- `Container Configuration` --references--> `Web Push`  [EXTRACTED]
  infra/podman-compose.yml → README.md
- `Client Entry Point` --references--> `MudBlazor`  [EXTRACTED]
  src/HomeGuard.Client/wwwroot/index.html → src/HomeGuard.Client/_Imports.razor
- `CI Workflow` --references--> `HomeGuard Service`  [EXTRACTED]
  .github/workflows/build.yml → infra/podman-compose.yml

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **HomeGuard Technology Stack** — blazor_wasm_pwa, aspnet_core, sqlite, podman [EXTRACTED 1.00]
- **HomeGuard External Integrations** — web_push, vapid_keys, fido2_passkeys, blob_storage, nextcloud_integration, google_calendar, ical_feed [INFERRED 0.85]
- **Client UI Dependencies** — mudblazor, vis_timeline [EXTRACTED 1.00]

## Communities (103 total, 14 thin omitted)

### Community 0 - "Warranty & Calendar"
Cohesion: 0.06
Nodes (41): ICalCalendar, CreateWarrantyRequest, CancellationToken, Guid, IResult, Task, WebApplication, NotificationRuleDto (+33 more)

### Community 1 - "Blob & Notifications"
Cohesion: 0.09
Nodes (24): CancellationToken, DateTimeOffset, Guid, IReadOnlyList, Task, IBlobEntryRepository, IRecurringRuleRepository, IScheduledJobRepository (+16 more)

### Community 2 - "Domain & Repos"
Cohesion: 0.10
Nodes (8): Entity, AppUser, Guid, List, PasskeyCredential, AppUserRepository, Credential, User

### Community 3 - "Equipment Module"
Cohesion: 0.06
Nodes (34): Fact, CreateEquipmentRequest, CancellationToken, Guid, IResult, Task, WebApplication, EquipmentDetailDto (+26 more)

### Community 4 - "Service Records"
Cohesion: 0.13
Nodes (19): CreateServiceRecordRequest, CancellationToken, Guid, IResult, Task, WebApplication, ServiceRecordDto, ServiceRecordEndpoints (+11 more)

### Community 5 - "Client API Clients"
Cohesion: 0.09
Nodes (34): CancellationToken, Guid, HttpClient, JsonSerializerOptions, List, Task, EquipmentApiClient, Json (+26 more)

### Community 6 - "Equipment Detail UI"
Cohesion: 0.06
Nodes (30): MeterReadingSection, Microsoft.AspNetCore.Components, route:/equipment/{Id:guid}, ServiceSection, DeleteEquipmentAsync, OnInitializedAsync, OpenEditDialog, DialogActions (+22 more)

### Community 7 - "Infrastructure Repos"
Cohesion: 0.20
Nodes (9): CancellationToken, DateOnly, DateTimeOffset, Guid, IReadOnlyList, Task, EquipmentRepository, ServiceRecordRepository (+1 more)

### Community 8 - "Tech Stack & Brand"
Cohesion: 0.07
Nodes (24): Blazor WASM PWA, Color, HomeGuard Brand Identity, HomeGuard.Client, HomeGuard.Client.Layout, HomeGuard.Client.Services, HomeGuard.Common, Microsoft.AspNetCore.Components.Forms (+16 more)

### Community 9 - "Service Records UI"
Cohesion: 0.08
Nodes (24): route:/service, DeleteAsync, LoadAsync, OnInitializedAsync, OnSavedAsync, ChildContent, EquipmentApiClient, EquipmentSummary (+16 more)

### Community 10 - "Warranties UI"
Cohesion: 0.08
Nodes (24): route:/warranties, DeleteAsync, LoadAsync, OnInitializedAsync, OnSavedAsync, ChildContent, EquipmentApiClient, EquipmentSummary (+16 more)

### Community 11 - "Settings UI"
Cohesion: 0.06
Nodes (34): IDialogService, MudTooltip, NotificationApiClient, PushStatus, route:/settings, AddDeviceAsync, ConfirmRevokeAsync, FlushAsync (+26 more)

### Community 12 - "Auth Endpoints"
Cohesion: 0.12
Nodes (24): AuthenticatorAssertionRawResponse, AuthenticatorAttestationRawResponse, IFido2, AddDeviceRequest, AuthEndpoints, CredentialDto, CancellationToken, Guid (+16 more)

### Community 13 - "Home Dashboard"
Cohesion: 0.06
Nodes (35): route:/, DaysColor, DaysLabel, Dispose, FlushAsync, LoadServiceAsync, LoadWarrantiesAsync, OnInitializedAsync (+27 more)

### Community 14 - "Startup Diagnostics"
Cohesion: 0.11
Nodes (13): Assembly, ConfigurationManager, IServiceProvider, IEnumerable, ILogger, int, List, Task (+5 more)

### Community 15 - "Equipment List UI"
Cohesion: 0.07
Nodes (26): route:/equipment, CategoryIcon, OnInitializedAsync, OpenAddDialog, OpenDetail, DialogActions, DialogContent, EquipmentApiClient (+18 more)

### Community 16 - "Auth Client"
Cohesion: 0.16
Nodes (13): Error, AuthApiClient, AuthResult, AuthResultDto, CredentialDto, CancellationToken, Guid, HttpClient (+5 more)

### Community 17 - "Client DB & Outbox"
Cohesion: 0.12
Nodes (12): IEnumerable, IJSRuntime, IReadOnlyList, T, Task, HomeGuardDb, OutboxEntryLocal, CancellationToken (+4 more)

### Community 18 - "NuGet Dependencies"
Cohesion: 0.09
Nodes (21): Google.Apis.Calendar.v3, Ical.Net, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.Configuration, Microsoft.Extensions.Configuration.Binder, Microsoft.Extensions.Configuration.EnvironmentVariables, Microsoft.Extensions.Configuration.UserSecrets, Microsoft.Extensions.Options.ConfigurationExtensions (+13 more)

### Community 19 - "EF Core & UnitOfWork"
Cohesion: 0.27
Nodes (6): IDisposable, SemaphoreSlim, CancellationToken, Task, HomeGuardUnitOfWork, WriteSemaphore

### Community 20 - "Integration Tests"
Cohesion: 0.09
Nodes (21): Microsoft.AspNetCore.Mvc.Testing, Microsoft.Bcl.Memory, Microsoft.Bcl.TimeProvider, Microsoft.IdentityModel.JsonWebTokens, Microsoft.IdentityModel.Tokens, FluentAssertions, Microsoft.CodeAnalysis.Analyzers, Microsoft.EntityFrameworkCore.InMemory (+13 more)

### Community 21 - "Login UI"
Cohesion: 0.10
Nodes (19): route:/login, LoginAsync, OnAfterRenderAsync, AuthApiClient, IJSRuntime, ISnackbar, MudAlert, MudButton (+11 more)

### Community 22 - "Background Services"
Cohesion: 0.26
Nodes (10): BackgroundService, IServiceScopeFactory, BlobSyncHostedService, CancellationToken, ILogger, Task, TimeSpan, JobRunnerService (+2 more)

### Community 23 - "Timeline Interop"
Cohesion: 0.13
Nodes (14): DotNetObjectReference, IAsyncDisposable, JSInvokable, IEnumerable, IJSRuntime, JsonSerializerOptions, string, Task (+6 more)

### Community 24 - "Timeline UI"
Cohesion: 0.06
Nodes (32): route:/timeline, BuildRows, DisposeAsync, FuzzyGetOrAdd, Lev, LoadDataAsync, Norm, OnAfterRenderAsync (+24 more)

### Community 25 - "Sync Processor"
Cohesion: 0.40
Nodes (3): ModelBuilder, HomeGuard.Infrastructure.Migrations, RecurringRuleAnchorToPurchaseDate

### Community 26 - "Blob Storage Service"
Cohesion: 0.07
Nodes (29): Blazor Implementation Plan, Click on event button, CSS Classes (timeline.css), Data Model for Timeline, Detail Card (below timeline), Event point on the timeline, Files to create/modify, HomeGuard Timeline — Component Specification (+21 more)

### Community 27 - "Document Capture"
Cohesion: 0.13
Nodes (14): CapturedDocument, DocumentCapture, HomeGuard.Client.Model, IJSObjectReference, CapturePhoto, ClearCapture, DisposeAsync, OnAfterRenderAsync (+6 more)

### Community 28 - "Notification Scheduler"
Cohesion: 0.13
Nodes (13): CancellationToken, DateOnly, DateTimeOffset, Guid, string, Task, DateOnlyExtensions, JobTypes (+5 more)

### Community 29 - "API Dependencies"
Cohesion: 0.13
Nodes (14): Fido2.AspNet, Microsoft.AspNetCore.OpenApi, Serilog.Enrichers.Environment, Serilog.Exceptions.EntityFrameworkCore, Serilog.Extensions.Hosting, Serilog.Extensions.Logging, Serilog.Settings.Configuration, Serilog.Sinks.Console (+6 more)

### Community 30 - "App Root & Router"
Cohesion: 0.15
Nodes (12): FocusOnNavigate, Found, LayoutView, MudDialogProvider, MudPopoverProvider, MudSnackbarProvider, MudThemeProvider, NotFound (+4 more)

### Community 31 - "Config Masker"
Cohesion: 0.18
Nodes (8): GeneratedRegex, Key, Regex, ConfigMasker, IConfiguration, IEnumerable, string, Value

### Community 32 - "Shared Projects"
Cohesion: 0.16
Nodes (11): Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, FluentAssertions, Microsoft.EntityFrameworkCore.InMemory, Microsoft.NET.Test.Sdk, NSubstitute (+3 more)

### Community 33 - "Startup Validator"
Cohesion: 0.26
Nodes (4): IWebHostEnvironment, IConfiguration, List, StartupValidator

### Community 34 - "Main Layout"
Cohesion: 0.15
Nodes (12): LayoutComponentBase, MudAppBar, MudDrawer, MudDrawerHeader, MudLayout, MudMainContent, NavMenu, MudContainer (+4 more)

### Community 35 - "Community 35"
Cohesion: 0.15
Nodes (12): CancellationToken, Guid, string, T, Task, DeletePayload, SyncOperationTypes, SyncProcessorService (+4 more)

### Community 36 - "EF Migrations"
Cohesion: 0.09
Nodes (11): Migration, MigrationBuilder, InitialCreate, Continuation, MigrationBuilder, MigrationBuilder, ServiceRecordAndRecurringRule, MigrationBuilder (+3 more)

### Community 37 - "Nav Menu"
Cohesion: 0.18
Nodes (10): MudNavLink, MudNavMenu, LogoutAsync, OnInitializedAsync, AuthApiClient, MudDivider, MudIcon, MudStack (+2 more)

### Community 38 - "Launch Settings"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, https (+1 more)

### Community 39 - "Client DB JS"
Cohesion: 0.38
Nodes (9): cacheDelete(), cacheGet(), cacheSet(), openDb(), outboxAdd(), outboxCount(), outboxGetPending(), outboxMarkDelivered() (+1 more)

### Community 40 - "PWA Manifest"
Cohesion: 0.20
Nodes (9): background_color, description, display, icons, name, prefer_related_applications, short_name, start_url (+1 more)

### Community 41 - "Document Capture JS"
Cohesion: 0.25
Nodes (4): ALLOWED, EXT_MAP, processFile(), readBase64()

### Community 42 - "API Auth Handler"
Cohesion: 0.25
Nodes (6): DelegatingHandler, HttpRequestMessage, HttpResponseMessage, ApiAuthHandler, CancellationToken, Task

### Community 43 - "Equipment Form Page"
Cohesion: 0.07
Nodes (29): RecurringRuleDialog, DeleteRuleAsync, DeleteServiceAsync, IntervalLabel, LoadRulesAsync, LoadServicesAsync, OnAfterRender, OnInitializedAsync (+21 more)

### Community 44 - "Equipment Form Shared"
Cohesion: 0.25
Nodes (7): MudDatePicker, MudGrid, MudItem, MudNumericField, MudSelect, MudSelectItem, MudTextField

### Community 46 - "Community 46"
Cohesion: 0.16
Nodes (13): IFormFile, BlobEndpoints, CalendarFeedEndpoints, CancellationToken, Guid, HttpContext, IResult, Task (+5 more)

### Community 48 - "Model Snapshot"
Cohesion: 0.33
Nodes (4): ModelSnapshot, ModelBuilder, HomeGuard.Infrastructure.Migrations, HomeGuardDbContextModelSnapshot

### Community 49 - "Client Dependencies"
Cohesion: 0.33
Nodes (5): Microsoft.AspNetCore.Components.WebAssembly, Microsoft.AspNetCore.Components.WebAssembly.DevServer, Microsoft.Extensions.Http, MudBlazor, Microsoft.NET.Sdk.BlazorWebAssembly

### Community 50 - "Auth JS (WebAuthn)"
Cohesion: 0.60
Nodes (4): authenticate(), _base64ToBuffer(), _bufferToBase64(), register()

### Community 51 - "Timeline JS"
Cohesion: 0.14
Nodes (15): _addCardAction(), _buildInstance(), create(), fit(), _fitToData(), _fmtDate(), _fmtNum(), _getTicks() (+7 more)

### Community 52 - "DB Startup Extensions"
Cohesion: 0.40
Nodes (3): Task, WebApplication, DatabaseStartupExtensions

### Community 53 - "Passkey Auth Setup"
Cohesion: 0.40
Nodes (3): IConfiguration, IServiceCollection, PasskeyAuthExtensions

### Community 54 - "Initial Migration"
Cohesion: 0.40
Nodes (3): ModelBuilder, HomeGuard.Infrastructure.Migrations, InitialCreate

### Community 55 - "Continuation Migration"
Cohesion: 0.40
Nodes (3): Continuation, ModelBuilder, HomeGuard.Infrastructure.Migrations

### Community 58 - "Form Models"
Cohesion: 0.40
Nodes (4): EquipmentFormModel, RecurringRuleFormModel, ServiceRecordFormModel, WarrantyFormModel

### Community 71 - "Community 71"
Cohesion: 0.20
Nodes (9): CalendarEventDto, CancellationToken, Guid, Stream, Task, IBlobStorage, ICalendarProvider, INotificationSender (+1 more)

### Community 72 - "Community 72"
Cohesion: 0.11
Nodes (17): Architecture Patterns, Backend, Domain Models, Equipment, Frontend, HomeGuard — Claude Code Context, Key Conventions, MeterReading / MeterUnit (+9 more)

### Community 73 - "Community 73"
Cohesion: 0.25
Nodes (10): PushNotification, CancellationToken, Guid, IEnumerable, ILogger, string, Task, PushSubscriptionEntity (+2 more)

### Community 74 - "Community 74"
Cohesion: 0.09
Nodes (22): CloseAsync, OnEquipmentChanged, OnParametersSetAsync, OnVisibleChanged, DialogActions, DialogContent, EquipmentSummary, ISnackbar (+14 more)

### Community 75 - "Community 75"
Cohesion: 0.20
Nodes (11): Blob Storage, .NET 9.0, FIDO2 Passkeys, GitHub Container Registry, CI Workflow, Google Calendar, HomeGuard Service, Container Configuration (+3 more)

### Community 76 - "Community 76"
Cohesion: 0.18
Nodes (10): 1. Prerequisites, 2. VAPID keys (Web Push), 3. Database migration, 4. Run (development), 5. Run with Podman (production), Getting started, HomeGuard, iCal feed (+2 more)

### Community 77 - "Community 77"
Cohesion: 0.10
Nodes (20): MudAutocomplete, OpenAdd, OpenEdit, DialogActions, DialogContent, EquipmentSummary, MudButton, MudDatePicker (+12 more)

### Community 78 - "Community 78"
Cohesion: 0.31
Nodes (7): CalendarService, CancellationToken, ILogger, string, Task, GoogleCalendarOptions, GoogleCalendarProvider

### Community 79 - "Community 79"
Cohesion: 0.22
Nodes (6): DbContext, IDesignTimeDbContextFactory, HomeGuardDbContextFactory, ModelBuilder, HomeGuardDbContext, ProcessedOperation

### Community 86 - "Community 86"
Cohesion: 0.27
Nodes (8): BlobEntryRepository, MeterReadingRepository, RecurringRuleRepository, CancellationToken, Guid, T, Task, RepositoryBase

### Community 87 - "Community 87"
Cohesion: 0.08
Nodes (27): CreateMeterReadingRequest, CancellationToken, Guid, IResult, MeterReading, Task, WebApplication, MeterReadingDto (+19 more)

### Community 88 - "Community 88"
Cohesion: 0.10
Nodes (19): DeleteAsync, LoadAsync, OnInitializedAsync, OnSavedAsync, Guid, MudButton, MudChip, MudCollapse (+11 more)

### Community 89 - "Community 89"
Cohesion: 0.40
Nodes (4): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Relational, Serilog, Microsoft.NET.Sdk

### Community 90 - "Community 90"
Cohesion: 0.10
Nodes (27): CreateRecurringRuleRequest, CancellationToken, Guid, IResult, Task, WebApplication, PredictedEventDto, RecurringRuleDto (+19 more)

### Community 91 - "Community 91"
Cohesion: 0.40
Nodes (3): ModelBuilder, HomeGuard.Infrastructure.Migrations, ServiceRecordAndRecurringRule

### Community 92 - "Community 92"
Cohesion: 0.12
Nodes (16): DeleteAsync, LoadAsync, OnInitializedAsync, OnSavedAsync, EquipmentSummary, MeterReadingApiClient, MeterReadingDialog, MeterReadingDto (+8 more)

### Community 93 - "Community 93"
Cohesion: 0.39
Nodes (5): SyncAck, CancellationToken, Guid, Task, ProcessedOperationStore

### Community 94 - "Community 94"
Cohesion: 0.40
Nodes (3): IConfiguration, IServiceCollection, InfrastructureServiceExtensions

### Community 95 - "Community 95"
Cohesion: 0.12
Nodes (16): OpenAdd, OpenEdit, DialogActions, DialogContent, EquipmentSummary, MudButton, MudDatePicker, MudDialog (+8 more)

### Community 96 - "Community 96"
Cohesion: 0.12
Nodes (15): MudSwitch, OpenAdd, OpenEdit, DialogActions, DialogContent, MudButton, MudDialog, MudGrid (+7 more)

### Community 97 - "Community 97"
Cohesion: 0.20
Nodes (8): DateOnly, enabled, Guid, IEnumerable, List, offset, ServiceRecord, ServiceStatus

### Community 98 - "Community 98"
Cohesion: 0.40
Nodes (3): ModelBuilder, HomeGuard.Infrastructure.Migrations, MeterReading

### Community 99 - "Community 99"
Cohesion: 0.18
Nodes (5): DateTimeOffset, int, TimeSpan, ScheduledJob, ScheduledJobRepository

### Community 100 - "Community 100"
Cohesion: 0.23
Nodes (8): IBrowserFile, long, BlobApiClient, BlobUploadResult, CancellationToken, Guid, HttpClient, Task

### Community 101 - "Community 101"
Cohesion: 0.36
Nodes (6): CancellationToken, DateOnly, Guid, IReadOnlyList, Task, IServiceRecordRepository

### Community 102 - "Community 102"
Cohesion: 0.40
Nodes (3): CancellationToken, Task, RecurringRuleMaterializationService

## Knowledge Gaps
- **617 isolated node(s):** `node`, `CredentialDto`, `PendingRegistration`, `PendingAddDevice`, `NotificationRuleRequest` (+612 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `SyncProcessorService` connect `Community 35` to `Warranty & Calendar`, `Equipment Module`, `Community 71`?**
  _High betweenness centrality (0.054) - this node is a cross-community bridge._
- **Why does `IUnitOfWork` connect `Auth Endpoints` to `Warranty & Calendar`, `Equipment Module`, `Service Records`, `Community 102`, `Community 71`, `Community 46`, `EF Core & UnitOfWork`, `Community 87`, `Community 90`, `Notification Scheduler`?**
  _High betweenness centrality (0.054) - this node is a cross-community bridge._
- **Why does `WarrantyService` connect `Warranty & Calendar` to `Community 35`, `Auth Endpoints`?**
  _High betweenness centrality (0.031) - this node is a cross-community bridge._
- **What connects `node`, `CredentialDto`, `PendingRegistration` to the rest of the system?**
  _617 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Warranty & Calendar` be split into smaller, more focused modules?**
  _Cohesion score 0.05582603050957481 - nodes in this community are weakly interconnected._
- **Should `Blob & Notifications` be split into smaller, more focused modules?**
  _Cohesion score 0.08585858585858586 - nodes in this community are weakly interconnected._
- **Should `Domain & Repos` be split into smaller, more focused modules?**
  _Cohesion score 0.10256410256410256 - nodes in this community are weakly interconnected._