// DevQuest service worker — offline support for the Blazor WebAssembly client.
// Strategy: runtime cache-first for same-origin GETs; APIs are never cached.
// First visit requires network; afterwards previously-loaded assets work offline.

const CACHE = 'devquest-v7';  // bump on every deploy so clients refresh  // bump on every deploy so clients refresh

self.addEventListener('install', event => {
    event.waitUntil(caches.open(CACHE).then(c => c.addAll(['/', '/manifest.json'])));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        // Drop old caches.
        const names = await caches.keys();
        await Promise.all(names.filter(n => n !== CACHE).map(n => caches.delete(n)));
        await self.clients.claim();
    })());
});

self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;
    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;
    // Never cache API responses (progress, tutor, code runs).
    if (url.pathname.startsWith('/api/')) return;
    // Never cache Blazor's SignalR circuit endpoint.
    if (url.pathname.includes('_blazor')) return;

    event.respondWith((async () => {
        const cache = await caches.open(CACHE);
        const cached = await cache.match(req);
        const network = fetch(req).then(response => {
            if (response && response.ok) cache.put(req, response.clone());
            return response;
        }).catch(() => undefined);
        // Serve cache immediately if present; refresh in background.
        if (cached) {
            event.waitUntil(network);
            return cached;
        }
        const response = await network;
        return response || new Response('offline', { status: 503 });
    })());
});
