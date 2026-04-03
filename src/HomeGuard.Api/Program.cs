var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// TODO: builder.Services.AddHomeGuardInfrastructure(builder.Configuration);
// TODO: builder.Services.AddHomeGuardApplication();
// TODO: builder.Services.AddPasskeyAuth();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// TODO: app.MapEquipmentEndpoints();
// TODO: app.MapWarrantyEndpoints();
// TODO: app.MapAuthEndpoints();

// iCal feed for Family Wall / NextCloud
// TODO: app.MapGet("/api/calendar/feed.ics", CalendarFeedHandler.Handle);

app.Run();
