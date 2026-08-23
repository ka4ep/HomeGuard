using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using HomeGuard.Client;
using HomeGuard.Client.Services;

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
// ResourcesPath обязателен: файлы лежат в Resources/, поэтому набор ресурсов называется
// HomeGuard.Client.Resources.Strings. Без него локализатор ищет HomeGuard.Client.Strings,
// не находит — и по своему контракту возвращает сам ключ, то есть на экране появляются
// Nav_Equipment вместо «Техника».
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddHomeGuardClientServices(apiUri.ToString());

// Every ILogger<T> call app-wide also lands in the JS-side ring buffer a phone-side
// error report (or the Settings "send logs" button) can ship to the server — see
// BrowserBufferLoggerProvider's own header comment for why.
builder.Services.AddSingleton<ILoggerProvider>(sp =>
    new BrowserBufferLoggerProvider(sp.GetRequiredService<IJSRuntime>()));

// A hosted Blazor WASM app picks up "Production" as its own environment from the
// server it's served by (there's no ASPNETCORE_ENVIRONMENT of its own), and Production
// defaults the minimum log level higher than Information — silently dropping every
// LogInformation call this whole logging feature depends on, provider notwithstanding.
// Pinned explicitly so an actual LAN run (Production, no run-dev script) logs the same
// as a dev session instead of the buffer just staying empty.
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();

// Культуру нужно поставить до запуска хоста: на ней завязаны и ресурсы, и форматы
// дат и чисел, а внутри уже запущенного WASM-хоста она не меняется — отсюда reload
// при переключении языка.
var js = host.Services.GetRequiredService<IJSRuntime>();

// Bypasses ILogger/BrowserBufferLoggerProvider entirely and *awaits* the JS call
// directly — the fire-and-forget version behind ILogger left no way to tell "the level
// filtered it" from "diagnostics.js never loaded" from "the interop call itself failed"
// apart when diag.html showed nothing. This one only has one way to not show up: the
// __hgLog global isn't there, meaning wwwroot/js/diagnostics.js's <script> tag in
// index.html didn't load/run on this device at all.
try
{
    await js.InvokeVoidAsync("__hgLog", "Information", "Boot",
        $"HomeGuard client booted at {DateTimeOffset.Now}");
}
catch
{
    // Nothing to fall back to here — this *is* the fallback path's own probe.
}

LanguagePreference.Apply(await LanguagePreference.ResolveStartupAsync(js));

await host.RunAsync();
