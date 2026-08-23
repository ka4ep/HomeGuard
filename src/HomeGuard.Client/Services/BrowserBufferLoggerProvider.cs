using Microsoft.JSInterop;

namespace HomeGuard.Client.Services;

/// <summary>
/// Forwards every <c>ILogger&lt;T&gt;</c> call app-wide into the JS-side ring buffer
/// (<c>window.__hgLogBuffer</c>, wwwroot/js/diagnostics.js) instead of just the browser
/// console. The point: a phone screen can't be copy-pasted from, so anything worth
/// logging with the standard .NET ILogger convention is now also shippable to the
/// server — either automatically (attached to an error report) or on demand (Settings'
/// "Send logs to server" button / diag.html) — without instrumenting individual call
/// sites beyond adding the odd ILogger.LogInformation() at a point worth narrating.
///
/// Fire-and-forget *async* interop (InvokeVoidAsync), not the synchronous
/// IJSInProcessRuntime variant this started as: a sync call made from inside a call
/// site that itself is mid-await on another JS interop operation (ApiAuthHandler logs
/// right after awaiting the fetch-backed SendAsync, for one) risks the exact kind of
/// interop reentrancy failure this whole thing exists to catch. Async fire-and-forget
/// never blocks the caller and never throws back into it either way.
/// </summary>
public sealed class BrowserBufferLoggerProvider(IJSRuntime js) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BrowserBufferLogger(js, categoryName);

    public void Dispose() { }
}

internal sealed class BrowserBufferLogger(IJSRuntime js, string category) : ILogger
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

        _ = LogAsync(logLevel, message);
    }

    private async Task LogAsync(LogLevel level, string message)
    {
        try
        {
            await js.InvokeVoidAsync("__hgLog", level.ToString(), category, message);
        }
        catch
        {
            // Interop can fail early in boot (JS module not ready yet), mid-navigation
            // (the JS side torn down), or if the runtime itself is in a bad state —
            // logging must never throw, awaited or not.
        }
    }
}
