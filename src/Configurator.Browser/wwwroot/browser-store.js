let request;
let generation = 0;

export function cancelRead() {
    generation++;
    request?.abort();
}

globalThis.addEventListener('configurator-custody-abandoned', cancelRead);

export async function readAsset(path, maximumBytes) {
    if (path !== 'index.json' && !/^definitions\/[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?\/[1-9][0-9]{0,18}\.json$/.test(path)) {
        throw new Error('store-path-invalid');
    }
    if (maximumBytes !== 4194304 || !globalThis.configuratorPwa?.canReadPublicAssets()) {
        throw new Error('store-unavailable');
    }
    const root = new URL('definition-store/', document.baseURI);
    const url = new URL(path, root);
    if (root.origin !== location.origin || url.origin !== root.origin || !url.href.startsWith(root.href) || url.search || url.hash) {
        throw new Error('store-path-invalid');
    }
    cancelRead();
    const operation = generation;
    const cancellation = new AbortController();
    request = cancellation;
    const timeout = setTimeout(() => cancellation.abort(), 10000);
    let reader;
    const chunks = [];
    try {
        const response = await fetch(url, {
            credentials: 'omit', referrerPolicy: 'no-referrer', redirect: 'error', signal: cancellation.signal
        });
        if (!response.ok || response.type === 'opaque' || !response.body) throw new Error('store-unavailable');
        const length = response.headers.get('Content-Length');
        if (length !== null && (!/^\d+$/.test(length) || Number(length) > maximumBytes)) throw new Error('store-size-invalid');
        reader = response.body.getReader();
        let total = 0;
        while (true) {
            const item = await reader.read();
            if (item.done) break;
            chunks.push(item.value);
            total += item.value.byteLength;
            if (total > maximumBytes || operation !== generation || !globalThis.configuratorPwa.canReadPublicAssets()) throw new Error('store-unavailable');
        }
        if (!total || operation !== generation || !globalThis.configuratorPwa.canReadPublicAssets()) throw new Error('store-unavailable');
        const bytes = new Uint8Array(total);
        let offset = 0;
        for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
        return bytes;
    } catch {
        throw new Error('store-unavailable');
    } finally {
        clearTimeout(timeout);
        cancellation.abort();
        if (reader) { try { await reader.cancel(); } catch { } reader.releaseLock(); }
        for (const chunk of chunks) chunk.fill(0);
        if (request === cancellation) request = undefined;
    }
}
