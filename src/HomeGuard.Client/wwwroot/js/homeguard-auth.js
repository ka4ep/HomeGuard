// homeguard-auth.js
// WebAuthn / Passkeys browser-side flow.
// Called from Login.razor via IJSRuntime.

window.homeGuardAuth = {

    // ── Registration ──────────────────────────────────────────────────────────

    async register(optionsJson) {
        const options = JSON.parse(optionsJson);

        // Convert base64url fields to ArrayBuffer as required by the browser API.
        options.challenge = this._base64ToBuffer(options.challenge);
        options.user.id   = this._base64ToBuffer(options.user.id);

        if (options.excludeCredentials) {
            options.excludeCredentials = options.excludeCredentials.map(c => ({
                ...c,
                id: this._base64ToBuffer(c.id),
            }));
        }

        let credential;
        try {
            credential = await navigator.credentials.create({ publicKey: options });
        } catch (e) {
            // e.name is the standardised DOMException name (NotSupportedError,
            // NotAllowedError, …) — stable across browsers, unlike e.message, which
            // varies by vendor and is often too terse to act on ("The operation is not
            // supported."). Login.razor maps the name to something the household can
            // actually do something with; detail rides along for the log/console.
            return { error: e.name, detail: e.message };
        }

        // Serialize the response back to base64url for JSON transport.
        return {
            id:    credential.id,
            rawId: this._bufferToBase64(credential.rawId),
            type:  credential.type,
            response: {
                attestationObject: this._bufferToBase64(credential.response.attestationObject),
                clientDataJSON:    this._bufferToBase64(credential.response.clientDataJSON),
            },
            extensions: credential.getClientExtensionResults(),
        };
    },

    // ── Authentication ────────────────────────────────────────────────────────

    async authenticate(optionsJson) {
        const options = JSON.parse(optionsJson);

        options.challenge = this._base64ToBuffer(options.challenge);

        if (options.allowCredentials) {
            options.allowCredentials = options.allowCredentials.map(c => ({
                ...c,
                id: this._base64ToBuffer(c.id),
            }));
        }

        let assertion;
        try {
            assertion = await navigator.credentials.get({ publicKey: options });
        } catch (e) {
            // e.name is the standardised DOMException name (NotSupportedError,
            // NotAllowedError, …) — stable across browsers, unlike e.message, which
            // varies by vendor and is often too terse to act on ("The operation is not
            // supported."). Login.razor maps the name to something the household can
            // actually do something with; detail rides along for the log/console.
            return { error: e.name, detail: e.message };
        }

        return {
            id:    assertion.id,
            rawId: this._bufferToBase64(assertion.rawId),
            type:  assertion.type,
            response: {
                authenticatorData: this._bufferToBase64(assertion.response.authenticatorData),
                clientDataJSON:    this._bufferToBase64(assertion.response.clientDataJSON),
                signature:         this._bufferToBase64(assertion.response.signature),
                userHandle:        assertion.response.userHandle
                    ? this._bufferToBase64(assertion.response.userHandle)
                    : null,
            },
        };
    },

    // ── Helpers ───────────────────────────────────────────────────────────────

    _base64ToBuffer(base64url) {
        const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
        const binary = atob(base64);
        const buffer = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            buffer[i] = binary.charCodeAt(i);
        }
        return buffer.buffer;
    },

    _bufferToBase64(buffer) {
        const bytes  = new Uint8Array(buffer);
        let binary   = '';
        for (const b of bytes) binary += String.fromCharCode(b);
        return btoa(binary)
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=/g, '');
    },

    isSupported() {
        return !!(window.PublicKeyCredential && navigator.credentials);
    },
};
