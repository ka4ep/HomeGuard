// homeguard-push.js
// Web Push subscription helper called from Settings.razor.

window.homeGuardPush = {

    getPermission() {
        if (!('Notification' in window)) return 'unsupported';
        return Notification.permission;
    },

    async subscribe(vapidPublicKey) {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;

        const permission = await Notification.requestPermission();
        if (permission !== 'granted') return null;

        const registration = await navigator.serviceWorker.ready;

        const sub = await registration.pushManager.subscribe({
            userVisibleOnly:      true,
            applicationServerKey: this._urlBase64ToUint8Array(vapidPublicKey),
        });

        const json = sub.toJSON();
        return {
            endpoint: json.endpoint,
            p256dh:   json.keys.p256dh,
            auth:     json.keys.auth,
        };
    },

    _urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64  = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw     = atob(base64);
        return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
    },
};
