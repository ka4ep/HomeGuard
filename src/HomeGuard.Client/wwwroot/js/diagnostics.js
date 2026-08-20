// wwwroot/js/diagnostics.js — plain script, loaded first, so listeners are attached
// before Blazor boots. Catches what a phone screen can't: sends the actual error and
// stack to the server log instead of relying on someone transcribing a red banner.
(function () {
    function send(payload) {
        try {
            fetch('/api/diagnostics/client-error', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify(Object.assign({
                    url: location.href,
                    userAgent: navigator.userAgent,
                }, payload)),
            }).catch(function () { /* best-effort — never let reporting itself throw */ });
        } catch { /* ignore */ }
    }

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
