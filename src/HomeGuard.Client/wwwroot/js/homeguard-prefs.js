// Device-local preferences: language and list density.
//
// These are per-device on purpose. The language also lives on the account (the server
// renders push notifications and the calendar feed, where there is no browser to ask),
// but the copy here is what the app starts in before any request has been made.
// Density is device-only — a phone and a desktop want different answers.
window.homeGuardPrefs = {
    get(key) {
        try { return localStorage.getItem(key); } catch { return null; }
    },

    set(key, value) {
        try { localStorage.setItem(key, value); } catch { /* private mode, ignore */ }
    },

    remove(key) {
        try { localStorage.removeItem(key); } catch { /* ignore */ }
    },

    // "ru-RU" → normalised on the .NET side; null when the browser will not say.
    browserLanguage() {
        return navigator.language || (navigator.languages && navigator.languages[0]) || null;
    },

    // Matches the desktop breakpoint in DESIGN.md. Decides the default density on a
    // device that has never chosen one.
    isWideViewport() {
        return window.matchMedia('(min-width: 900px)').matches;
    },

    reload() {
        location.reload();
    },
};
