// Service worker: its only job is showing push notifications and opening the site when one is clicked.
// It must live at the site root (/sw.js) so that it covers the whole site. It caches nothing.

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('push', event => {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data ? event.data.text() : '' };
    }

    // Every push must show a notification (browsers, iPhones in particular, punish silent pushes).
    event.waitUntil(self.registration.showNotification(data.title || 'PandaHunt IV', {
        body: data.body || '',
        icon: '/img/icon-192.png',
        tag: data.tag || undefined,
        data: { url: data.url || '/' },
    }));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();

    let target = new URL('/', self.location.origin);
    try {
        const url = new URL(event.notification.data && event.notification.data.url || '/', self.location.origin);
        if (url.origin === self.location.origin)
            target = url;
    } catch { /* keep the home page */ }

    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of windows) {
            if (new URL(client.url).origin === self.location.origin && 'focus' in client) {
                await client.focus();
                if ('navigate' in client)
                    await client.navigate(target.href);
                return;
            }
        }
        await self.clients.openWindow(target.href);
    })());
});
