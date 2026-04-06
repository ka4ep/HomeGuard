using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using HomeGuard.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeGuard.Infrastructure.Calendar;

public sealed class GoogleCalendarOptions
{
    public const string Section = "Calendar:Google";

    /// <summary>Path to the service account JSON file, or the JSON content itself.</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>Calendar ID to write events to. Defaults to "primary".</summary>
    public string CalendarId { get; set; } = "primary";

    public bool Enabled { get; set; }
}

/// <summary>
/// Writes warranty/service events to Google Calendar using a Service Account.
/// Uses extended properties (private scope) to tag events as ours.
///
/// Only registered in DI when <see cref="GoogleCalendarOptions.Enabled"/> is true.
/// </summary>
public sealed class GoogleCalendarProvider : ICalendarProvider
{
    public string ProviderName => "Google";

    private readonly GoogleCalendarOptions _options;
    private readonly ILogger<GoogleCalendarProvider> _logger;

    public GoogleCalendarProvider(
        IOptions<GoogleCalendarOptions> options,
        ILogger<GoogleCalendarProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UpsertEventAsync(CalendarEventDto evt, CancellationToken ct = default)
    {
        var service = await BuildServiceAsync(ct);

        var gEvent = new Event
        {
            Summary     = evt.Title,
            Description = evt.Description,
            Start       = new EventDateTime { Date = evt.Date.ToString("yyyy-MM-dd") },
            End         = new EventDateTime { Date = evt.Date.AddDays(1).ToString("yyyy-MM-dd") },
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string>
                {
                    ["homeGuardTag"] = evt.HomeGuardTag
                }
            }
        };

        if (evt.ExternalId is not null)
        {
            // Update existing.
            var request = service.Events.Update(gEvent, _options.CalendarId, evt.ExternalId);
            var updated = await request.ExecuteAsync(ct);
            _logger.LogDebug("Google Calendar event updated: {EventId}", updated.Id);
            return updated.Id;
        }
        else
        {
            // Insert new.
            var request  = service.Events.Insert(gEvent, _options.CalendarId);
            var inserted = await request.ExecuteAsync(ct);
            _logger.LogDebug("Google Calendar event created: {EventId}", inserted.Id);
            return inserted.Id;
        }
    }

    public async Task DeleteEventAsync(string externalId, CancellationToken ct = default)
    {
        var service = await BuildServiceAsync(ct);
        await service.Events.Delete(_options.CalendarId, externalId).ExecuteAsync(ct);
        _logger.LogDebug("Google Calendar event deleted: {EventId}", externalId);
    }

    // ── Service factory ───────────────────────────────────────────────────────

    private async Task<CalendarService> BuildServiceAsync(CancellationToken ct)
    {
        GoogleCredential credential;

        if (_options.ServiceAccountJson.TrimStart().StartsWith('{'))
        {
            // Inline JSON content — CredentialFactory replaces deprecated FromJson().
            credential = CredentialFactory
                .FromJson<ServiceAccountCredential>(_options.ServiceAccountJson)
                .ToGoogleCredential()
                .CreateScoped(CalendarService.Scope.Calendar);
        }
        else
        {
            // File path.
            var json = await File.ReadAllTextAsync(_options.ServiceAccountJson, ct);
            credential = CredentialFactory
                .FromJson<ServiceAccountCredential>(json)
                .ToGoogleCredential()
                .CreateScoped(CalendarService.Scope.Calendar);
        }

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = "HomeGuard",
        });
    }
}
