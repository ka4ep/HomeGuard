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

var host = builder.Build();

// Культуру нужно поставить до запуска хоста: на ней завязаны и ресурсы, и форматы
// дат и чисел, а внутри уже запущенного WASM-хоста она не меняется — отсюда reload
// при переключении языка.
var js = host.Services.GetRequiredService<IJSRuntime>();
LanguagePreference.Apply(await LanguagePreference.ResolveStartupAsync(js));

await host.RunAsync();
