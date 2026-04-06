// homeguard-db.js
// IndexedDB wrapper for the Blazor client.
// Exposes a simple key-value store and an Outbox queue.
// Called via IJSRuntime from HomeGuardDb.cs.

const DB_NAME    = 'HomeGuard';
const DB_VERSION = 1;

let _db = null;

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
