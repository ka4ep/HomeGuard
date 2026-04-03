// Production service worker.
// Blazor replaces the asset list in self.assetsManifest at publish time.

const cacheNamePrefix = 'homeguard-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm$/, /\.html$/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.ico$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('push', event => event.waitUntil(onPush(event)));
self.addEventListener('notificationclick', event => event.waitUntil(onNotificationClick(event)));

async function onInstall(event) {
    self.skipWaiting();
    const assetsRequests = self.assetsManifest.assets
        .filter(a => offlineAssetsInclude.some(p => p.test(a.url)))
        .filter(a => !offlineAssetsExclude.some(p => p.test(a.url)))
        .map(a => new Request(a.url, { integrity: a.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(c => c.addAll(assetsRequests));
}

async function onActivate(event) {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(k => k.startsWith(cacheNamePrefix) && k !== cacheName)
        .map(k => caches.delete(k)));
}

async function onFetch(event) {
    if (event.request.method !== 'GET') return fetch(event.request);
    const shouldServeIndexHtml = event.request.mode === 'navigate';
    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cachedResponse = await caches.match(request);
    return cachedResponse ?? fetch(event.request);
}

// --- Web Push ---

async function onPush(event) {
    const payload = event.data?.json() ?? {};
    await self.registration.showNotification(payload.title ?? 'HomeGuard', {
        body: payload.body ?? '',
        icon: 'icon-192.png',
        badge: 'icon-192.png',
        tag: payload.tag ?? 'homeguard',
        data: { url: payload.url ?? '/' },
    });
}

async function onNotificationClick(event) {
    event.notification.close();
    const url = event.notification.data?.url ?? '/';
    const client = (await self.clients.matchAll({ type: 'window' })).find(c => c.url === url);
    if (client) {
        await client.focus();
    } else {
        await self.clients.openWindow(url);
    }
}
