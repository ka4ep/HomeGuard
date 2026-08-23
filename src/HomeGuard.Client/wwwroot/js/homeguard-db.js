// homeguard-db.js
// IndexedDB wrapper for the Blazor client.
// Exposes a simple key-value store and an Outbox queue.
// Called via IJSRuntime from HomeGuardDb.cs.

const DB_NAME    = 'HomeGuard';
const DB_VERSION = 2;

let _db = null;

// A byte[] argument crossing the .NET->JS interop boundary doesn't consistently arrive
// as one JS type. Confirmed live (not just Uint8Array as first assumed): a byte[] that
// isn't statically typed as byte[] at the JS interop call site — true here, `data` is
// just one field of an anonymous object — arrives wrapped in Blazor's own interop wire
// format, {"__byte[]": "<base64>"}, not a raw Uint8Array. IndexedDB's structured-clone
// storage preserves whichever shape it was handed, so a value read back later can be
// any of them; this is the one place they all get reconciled to the base64 string
// HomeGuardDb.cs's GetBytesFromBase64() actually needs.
function toBase64(data) {
    if (typeof data === 'string') return data;
    if (data instanceof ArrayBuffer) data = new Uint8Array(data);
    if (data instanceof Uint8Array) {
        let binary = '';
        for (let i = 0; i < data.length; i++) binary += String.fromCharCode(data[i]);
        return btoa(binary);
    }
    if (data && typeof data === 'object' && typeof data['__byte[]'] === 'string') {
        return data['__byte[]'];
    }
    throw new Error('Unrecognized blob data shape: ' + Object.prototype.toString.call(data));
}

async function openDb() {
    if (_db) return _db;

    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);

        req.onupgradeneeded = e => {
            const db = e.target.result;

            // Outbox: pending operations to sync to the server.
            if (!db.objectStoreNames.contains('outbox')) {
                const outbox = db.createObjectStore('outbox', { keyPath: 'clientOperationId' });
                outbox.createIndex('by_status',    'status',    { unique: false });
                outbox.createIndex('by_createdAt', 'createdAt', { unique: false });
            }

            // Cache: local copies of server data for offline reads.
            if (!db.objectStoreNames.contains('cache')) {
                db.createObjectStore('cache', { keyPath: 'key' });
            }

            // Blob outbox: file uploads pending a server round-trip. Kept separate from
            // 'outbox' — that store's payloadJson goes through the batch JSON endpoint,
            // which is the wrong shape for a file that can be several MB.
            if (!db.objectStoreNames.contains('blobOutbox')) {
                const blobOutbox = db.createObjectStore('blobOutbox', { keyPath: 'clientOperationId' });
                blobOutbox.createIndex('by_createdAt', 'createdAt', { unique: false });
            }
        };

        req.onsuccess = e => { _db = e.target.result; resolve(_db); };
        req.onerror   = e => reject(e.target.error);
    });
}

// ── Outbox ────────────────────────────────────────────────────────────────────

window.homeGuardDb = {

    // Add an entry to the outbox. Status: 'pending' | 'delivering' | 'delivered' | 'failed'
    async outboxAdd(entry) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx    = db.transaction('outbox', 'readwrite');
            const store = tx.objectStore('outbox');
            const req   = store.put({ ...entry, status: 'pending' });
            req.onsuccess = () => resolve(req.result);
            req.onerror   = () => reject(req.error);
        });
    },

    // Get all pending entries ordered by createdAt.
    async outboxGetPending() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx      = db.transaction('outbox', 'readonly');
            const index   = tx.objectStore('outbox').index('by_createdAt');
            const results = [];
            const req     = index.openCursor();
            req.onsuccess = e => {
                const cursor = e.target.result;
                if (cursor) {
                    if (cursor.value.status === 'pending' || cursor.value.status === 'failed')
                        results.push(cursor.value);
                    cursor.continue();
                } else {
                    resolve(results);
                }
            };
            req.onerror = () => reject(req.error);
        });
    },

    // Mark entries as delivered and remove them from the outbox.
    async outboxMarkDelivered(clientOperationIds) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx    = db.transaction('outbox', 'readwrite');
            const store = tx.objectStore('outbox');
            let done = 0;
            for (const id of clientOperationIds) {
                const req = store.delete(id);
                req.onsuccess = () => { if (++done === clientOperationIds.length) resolve(); };
                req.onerror   = () => reject(req.error);
            }
            if (clientOperationIds.length === 0) resolve();
        });
    },

    // Mark an entry as failed (will be retried next sync).
    async outboxMarkFailed(clientOperationId) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx    = db.transaction('outbox', 'readwrite');
            const store = tx.objectStore('outbox');
            const get   = store.get(clientOperationId);
            get.onsuccess = () => {
                if (!get.result) return resolve();
                const entry = { ...get.result, status: 'failed' };
                const put   = store.put(entry);
                put.onsuccess = () => resolve();
                put.onerror   = () => reject(put.error);
            };
            get.onerror = () => reject(get.error);
        });
    },

    async outboxCount() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx  = db.transaction('outbox', 'readonly');
            const req = tx.objectStore('outbox').count();
            req.onsuccess = () => resolve(req.result);
            req.onerror   = () => reject(req.error);
        });
    },

    // ── Blob outbox ──────────────────────────────────────────────────────────────

    async blobOutboxAdd(entry) {
        const db = await openDb();
        // .NET's own byte[]-argument marshalling picks its own wire format for `data`
        // (a raw Uint8Array in some paths, a base64 string in others) — normalizing to
        // one shape here means blobOutboxGetPending never has to guess which one it's
        // reading back.
        const normalized = { ...entry, data: toBase64(entry.data) };
        return new Promise((resolve, reject) => {
            const tx    = db.transaction('blobOutbox', 'readwrite');
            const store = tx.objectStore('blobOutbox');
            const req   = store.put(normalized);
            req.onsuccess = () => resolve(req.result);
            req.onerror   = () => reject(req.error);
        });
    },

    // Get all pending blob uploads ordered by createdAt. `data` is always returned as a
    // base64 string — HomeGuardDb.cs's GetBytesFromBase64() depends on that — even for
    // rows written before blobOutboxAdd started normalizing on the way in. A row whose
    // `data` isn't convertible at all (corrupt beyond recovery) is dropped and deleted
    // instead of taking the whole flush down with it — see the CS2012-adjacent crash
    // this was written for: one bad queued capture blocked every future sync attempt.
    async blobOutboxGetPending() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx      = db.transaction('blobOutbox', 'readonly');
            const index   = tx.objectStore('blobOutbox').index('by_createdAt');
            const results = [];
            const toDrop  = [];
            const req     = index.openCursor();
            req.onsuccess = e => {
                const cursor = e.target.result;
                if (cursor) {
                    try {
                        results.push({ ...cursor.value, data: toBase64(cursor.value.data) });
                    } catch (err) {
                        console.warn('[homeguard-db] dropping unreadable blobOutbox entry',
                            cursor.value.clientOperationId, err);
                        toDrop.push(cursor.value.clientOperationId);
                    }
                    cursor.continue();
                } else {
                    Promise.all(toDrop.map(id => homeGuardDb.blobOutboxRemove(id)))
                        .catch(() => { /* best-effort cleanup — a failed delete just retries next time */ })
                        .then(() => resolve(results));
                }
            };
            req.onerror = () => reject(req.error);
        });
    },

    async blobOutboxRemove(clientOperationId) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx  = db.transaction('blobOutbox', 'readwrite');
            const req = tx.objectStore('blobOutbox').delete(clientOperationId);
            req.onsuccess = () => resolve();
            req.onerror   = () => reject(req.error);
        });
    },

    async blobOutboxCount() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx  = db.transaction('blobOutbox', 'readonly');
            const req = tx.objectStore('blobOutbox').count();
            req.onsuccess = () => resolve(req.result);
            req.onerror   = () => reject(req.error);
        });
    },

    // ── Cache ─────────────────────────────────────────────────────────────────

    async cacheSet(key, valueJson) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx  = db.transaction('cache', 'readwrite');
            const req = tx.objectStore('cache').put({ key, value: valueJson, savedAt: Date.now() });
            req.onsuccess = () => resolve();
            req.onerror   = () => reject(req.error);
        });
    },

    async cacheGet(key) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx  = db.transaction('cache', 'readonly');
            const req = tx.objectStore('cache').get(key);
            req.onsuccess = () => resolve(req.result ? req.result.value : null);
            req.onerror   = () => reject(req.error);
        });
    },

    async cacheDelete(key) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx  = db.transaction('cache', 'readwrite');
            const req = tx.objectStore('cache').delete(key);
            req.onsuccess = () => resolve();
            req.onerror   = () => reject(req.error);
        });
    },
};
