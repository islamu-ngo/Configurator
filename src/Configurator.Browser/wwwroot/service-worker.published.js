importScripts('./service-worker-assets.js');

const cacheName = `configurator-app-${self.assetsManifest.version}`;
const scope = new URL(self.registration.scope);
const publicAssets = new Set([
    'index.html', 'css/layers.css', 'css/reset.css', 'css/base.css', 'css/tokens.css',
    'css/mudblazor-overrides.css', 'css/components.css', 'css/utilities.css',
    'Configurator.Browser.styles.css', 'manifest.webmanifest',
    'pwa-lifecycle.js', 'browser-definition-source.js', 'browser-artifacts.js',
    'browser-appearance.js', 'browser-store.js', 'workspace-interactions.js', 'configuration-rendering.js',
    '_content/MudBlazor/MudBlazor.min.css', '_content/MudBlazor/MudBlazor.min.js',
    'icons/configurator-192.svg', 'icons/configurator-512.svg'
]);
const assets = self.assetsManifest.assets.filter(asset =>
    asset.url.startsWith('_framework/') || publicAssets.has(asset.url));
const requiredUrls = new Set(assets.map(asset => new URL(asset.url, scope).href));
const storeAssets = new Map(self.assetsManifest.assets.filter(asset => asset.url.startsWith('definition-store/'))
    .map(asset => [new URL(asset.url, scope).href, asset]));
const appUrls = new Set([...requiredUrls, ...storeAssets.keys()]);
const activeClients = new Set();
const permittedSources = new Map();

self.addEventListener('install', event => event.waitUntil((async () => {
    const existed = await caches.has(cacheName);
    const cache = await caches.open(cacheName);
    try {
        await cache.addAll(assets.map(asset => new Request(new URL(asset.url, scope),
            { integrity: asset.hash, credentials: 'omit', cache: 'reload' })));
    } catch (error) {
        if (!existed) await caches.delete(cacheName);
        throw error;
    }
})()));

self.addEventListener('activate', event => event.waitUntil((async () => {
    // Default worker activation waits for old tabs to close; no skipWaiting or automatic page reload.
    for (const name of await caches.keys()) {
        if (name.startsWith('configurator-app-') && name !== cacheName) await caches.delete(name);
    }
    await self.clients.claim();
})()));

self.addEventListener('message', event => event.waitUntil((async () => {
    if (!event.source?.id) return;
    if (event.data?.type === 'custody') {
        if (event.data.active === true) activeClients.add(event.source.id);
        else activeClients.delete(event.source.id);
        permittedSources.delete(event.source.id);
        event.ports[0]?.postMessage({ accepted: true });
    }
    if (event.data?.type === 'definition-source') {
        const url = new URL(event.data.url);
        const accepted = !activeClients.has(event.source.id) && self.navigator.onLine && url.protocol === 'https:' &&
            !url.username && !url.password && !url.hash;
        if (accepted) permittedSources.set(event.source.id, url.href);
        event.ports[0]?.postMessage({ accepted });
    }
    if (event.data?.type === 'status') {
        const cache = await caches.open(cacheName);
        const complete = (await Promise.all([...requiredUrls].map(url => cache.match(url))))
            .every(response => response !== undefined);
        event.ports[0]?.postMessage({ appPrepared: complete, offlineReady: complete, assets: [...appUrls] });
    }
})()));

self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);
    if (request.method !== 'GET') { event.respondWith(Response.error()); return; }
    if (appUrls.has(url.href) || request.mode === 'navigate' && url.origin === scope.origin && url.pathname.startsWith(scope.pathname)) {
        event.respondWith((async () => {
            const cache = await caches.open(cacheName);
            const key = request.mode === 'navigate' ? new URL('index.html', scope).href : url.href;
            const cached = await cache.match(key);
            if (cached) return cached;
            // Optional discovery cannot hold core readiness hostage; cache only deliberate public asset reads.
            const asset = storeAssets.get(key);
            if (!asset || activeClients.has(event.clientId) || !self.navigator.onLine || request.credentials !== 'omit') return Response.error();
            try {
                const response = await fetch(new Request(key,
                    { integrity: asset.hash, credentials: 'omit', referrerPolicy: 'no-referrer', redirect: 'error', cache: 'no-store' }));
                if (!response.ok) return Response.error();
                await cache.put(key, response.clone());
                return response;
            } catch { return Response.error(); }
        })());
        return;
    }
    // One explicitly admitted source request at clean entry; no runtime cache/network fallback.
    if (permittedSources.get(event.clientId) === url.href && !activeClients.has(event.clientId) &&
        self.navigator.onLine && request.credentials === 'omit' && request.cache === 'no-store') {
        permittedSources.delete(event.clientId);
        event.respondWith(fetch(request));
    } else {
        event.respondWith(Response.error());
    }
});
