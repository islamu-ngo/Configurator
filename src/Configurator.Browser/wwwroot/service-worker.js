// Development is deliberately online-only; published assets have a separate immutable-cache worker.
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('message', event => {
    if (event.data?.type === 'status') event.ports[0]?.postMessage({ appPrepared: true, offlineReady: false });
    if (event.data?.type === 'custody') event.ports[0]?.postMessage({ accepted: true });
    if (event.data?.type === 'definition-source') event.ports[0]?.postMessage({ accepted: true });
});
