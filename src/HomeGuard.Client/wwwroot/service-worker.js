// Dev-mode service worker — no caching, passes all requests through.
// The published version (service-worker.published.js) handles offline caching
// and Web Push subscriptions.
self.addEventListener('fetch', () => { });
