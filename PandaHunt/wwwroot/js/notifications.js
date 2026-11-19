// Browser notifications (Web Push): what this browser can do, subscribing and unsubscribing.
// The "Zapnout" button is handled here (not by Blazor): the permission prompt must be opened straight from the
// click, and the round trip through the server would be too late on iPhones.
window.pandaNotifications = (() => {
    let dotNet = null;
    let publicKey = null;

    function detect() {
        const ua = navigator.userAgent || '';
        const iPadOs = navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
        const platform = /iPhone|iPad|iPod/.test(ua) || iPadOs ? 'ios'
            : /Android/.test(ua) ? 'android'
            : /Windows/.test(ua) ? 'windows'
            : /Mac/.test(ua) ? 'mac'
            : /Linux|CrOS/.test(ua) ? 'linux'
            : 'other';
        const browser = /Edg\//.test(ua) ? 'Edge'
            : /OPR\/|Opera/.test(ua) ? 'Opera'
            : /SamsungBrowser/.test(ua) ? 'Samsung Internet'
            : /Firefox\/|FxiOS/.test(ua) ? 'Firefox'
            : /CriOS|Chrome\//.test(ua) ? 'Chrome'
            : /Safari\//.test(ua) ? 'Safari'
            : 'prohlížeč';
        const names = { ios: /iPad/.test(ua) || iPadOs ? 'iPad' : 'iPhone', android: 'Android', windows: 'Windows', mac: 'Mac', linux: 'Linux', other: 'Zařízení' };
        const standalone = window.navigator.standalone === true || window.matchMedia('(display-mode: standalone)').matches;
        const inApp = /FBAN|FBAV|Instagram|Messenger|Snapchat|TikTok|Line\//.test(ua);
        return {
            platform, browser, standalone, inApp,
            label: `${names[platform]} · ${browser}${standalone ? ' (aplikace)' : ''}`,
            secure: window.isSecureContext,
            supported: window.isSecureContext && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window,
            permission: 'Notification' in window ? Notification.permission : 'unsupported',
        };
    }

    function toBase64Url(buffer) {
        if (!buffer) return '';
        let text = '';
        for (const b of new Uint8Array(buffer)) text += String.fromCharCode(b);
        return btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function fromBase64Url(value) {
        const padded = (value + '='.repeat((4 - value.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
        return Uint8Array.from(atob(padded), c => c.charCodeAt(0));
    }

    function sameKey(subscription, key) {
        const current = subscription.options && subscription.options.applicationServerKey;
        return !!current && toBase64Url(current) === toBase64Url(key);
    }

    async function currentSubscription() {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;
        const registration = await navigator.serviceWorker.getRegistration('/');
        return registration ? await registration.pushManager.getSubscription() : null;
    }

    // Called right from the click. Opens the permission prompt first thing, then subscribes.
    async function enable() {
        const info = detect();
        if (!info.supported) return dotNet.invokeMethodAsync('OnPushFailed', 'unsupported');

        try {
            const permission = await Notification.requestPermission();
            if (permission !== 'granted') return dotNet.invokeMethodAsync('OnPushFailed', 'denied');

            await navigator.serviceWorker.register('/sw.js', { scope: '/' });
            const registration = await navigator.serviceWorker.ready;
            const key = fromBase64Url(publicKey);

            let subscription = await registration.pushManager.getSubscription();
            if (subscription && !sameKey(subscription, key)) {
                await subscription.unsubscribe();
                subscription = null;
            }
            subscription = subscription || await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });

            await dotNet.invokeMethodAsync('OnPushSubscribed', {
                endpoint: subscription.endpoint,
                p256dh: toBase64Url(subscription.getKey('p256dh')),
                auth: toBase64Url(subscription.getKey('auth')),
                label: info.label,
            });
        } catch (error) {
            console.error('Push subscription failed', error);
            await dotNet.invokeMethodAsync('OnPushFailed', 'error');
        }
    }

    document.addEventListener('click', event => {
        if (dotNet && event.target.closest('[data-push-enable]')) enable();
    });

    return {
        init(reference, key) {
            dotNet = reference;
            publicKey = key;
        },
        dispose() {
            dotNet = null;
        },
        async getInfo() {
            const info = detect();
            const subscription = info.supported ? await currentSubscription() : null;
            info.endpoint = subscription ? subscription.endpoint : null;
            return info;
        },
        // Turns notifications off in this browser. Returns the address that was subscribed (or null).
        async disable() {
            const subscription = await currentSubscription();
            if (!subscription) return null;
            const endpoint = subscription.endpoint;
            await subscription.unsubscribe();
            return endpoint;
        },
    };
})();
