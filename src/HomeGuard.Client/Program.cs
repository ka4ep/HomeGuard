using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using HomeGuard.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base — in production this is the same origin; in dev the Api runs separately.
var apiBase = builder.Configuration["ApiBaseAddress"]
              ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddMudServices();
builder.Services.AddHomeGuardClientServices(apiBase);

await builder.Build().RunAsync();
