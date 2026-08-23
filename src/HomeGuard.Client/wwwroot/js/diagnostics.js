// wwwroot/js/diagnostics.js — plain script, loaded first, so listeners are attached
// before Blazor boots. Catches what a phone screen can't: sends the actual error, its
// stack, and a trail of what led up to it (see __hgLog, called from the .NET side's
// BrowserBufferLoggerProvider — any ILogger<T> call anywhere in the app lands here) to
// the server log instead of relying on someone transcribing a red banner.
(function () {
    var MAX_LOG = 300;
    window.__hgLogBuffer = [];

    // Called from BrowserBufferLoggerProvider for every ILogger<T> call app-wide — kept
    // as a plain JS array (not routed through .NET) so it survives even if the WASM
    // runtime itself is what's crashing.
    window.__hgLog = function (level, category, message) {
        window.__hgLogBuffer.push({ t: new Date().toISOString(), level: level, category: category, message: message });
        if (window.__hgLogBuffer.length > MAX_LOG) window.__hgLogBuffer.shift();
    };

    function send(payload) {
        try {
            return fetch('/api/diagnostics/client-error', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify(Object.assign({
                    url: location.href,
                    userAgent: navigator.userAgent,
                    logs: window.__hgLogBuffer,
                }, payload)),
            }).catch(function () { /* best-effort — never let reporting itself throw */ });
        } catch { /* ignore */ }
    }

    // Settings' "Send logs to server" button — a report with no error, just the trail,
    // for exactly the case where something looked wrong but nothing actually threw.
    window.__hgFlushLogs = function (reason) {
        return send({ source: 'manual', message: reason || 'manual log flush' });
    };

    window.addEventListener('error', function (e) {
        send({
            source:  'window.onerror',
            message: e.message || String(e.error),
            stack:   e.error && e.error.stack,
        });
    });

    window.addEventListener('unhandledrejection', function (e) {
        var reason = e.reason;
        send({
            source:  'unhandledrejection',
            message: (reason && (reason.message || String(reason))) || 'unknown rejection',
            stack:   reason && reason.stack,
        });
    });
})();
