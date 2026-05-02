using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using HomeGuard.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Пустая строка или отсутствие ключа — тот же origin (продакшн в Podman).
// В Development appsettings перекрывает на http://localhost:5000.
var apiBase = builder.Configuration["ApiBaseAddress"];
var apiUri  = string.IsNullOrWhiteSpace(apiBase)
    ? new Uri(builder.HostEnvironment.BaseAddress)
    : new Uri(apiBase);

builder.Services.AddMudServices();
builder.Services.AddHomeGuardClientServices(apiUri.ToString());

await builder.Build().RunAsync();
