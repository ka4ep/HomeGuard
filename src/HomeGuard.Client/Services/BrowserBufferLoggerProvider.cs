using Microsoft.JSInterop;

namespace HomeGuard.Client.Services;

/// <summary>
/// Forwards every <c>ILogger&lt;T&gt;</c> call app-wide into the JS-side ring buffer
/// (<c>window.__hgLogBuffer</c>, wwwroot/js/diagnostics.js) instead of just the browser
/// console. The point: a phone screen can't be copy-pasted from, so anything worth
/// logging with the standard .NET ILogger convention is now also shippable to the
/// server — either automatically (attached to an error report) or on demand (Settings'
/// "Send logs to server" button) — without instrumenting individual call sites beyond
/// adding the odd ILogger.LogInformation() at a point actually worth narrating.
/// Synchronous JS interop (IJSInProcessRuntime) — WASM supports it, and a logger's
/// Log() method isn't async, so this avoids a fire-and-forget Task no one awaits.
/// </summary>
public sealed class BrowserBufferLoggerProvider(IJSInProcessRuntime js) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BrowserBufferLogger(js, categoryName);

    public void Dispose() { }
}

internal sealed class BrowserBufferLogger(IJSInProcessRuntime js, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    // Debug/Trace would flood a 300-entry buffer with framework noise before anything
    // useful survives to a flush — Information and up only.
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (exception is not null) message += " — " + exception;

        try
        {
            js.InvokeVoid("__hgLog", logLevel.ToString(), category, message);
        }
        catch
        {
            // Interop can fail early in boot (JS module not ready yet) or if the JS
            // runtime itself is what's in a bad state — logging must never throw.
        }
    }
}
