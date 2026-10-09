let currentRequest;
let generation = 0;

export function cancelFetch() {
    generation++;
    currentRequest?.abort();
}

globalThis.addEventListener('configurator-custody-abandoned', cancelFetch);

export async function fetchDefinition(source, maximumBytes) {
    const operation = ++generation;
    if (!globalThis.configuratorPwa?.canFetchDefinition()
        || !await globalThis.configuratorPwa.prepareDefinitionFetch(source)
        || operation !== generation || !globalThis.configuratorPwa.canFetchDefinition()) {
        throw new Error('source-offline-or-active');
    }
    currentRequest?.abort();
    const cancellation = new AbortController();
    currentRequest = cancellation;
    const timeout = setTimeout(() => cancellation.abort(), 10000);
    let reader;
    const chunks = [];
    try {
        const response = await fetch(source, {
            credentials: 'omit', referrerPolicy: 'no-referrer', cache: 'no-store',
            redirect: 'error', signal: cancellation.signal
        });
        if (!response.ok || response.type === 'opaque' || !response.body) throw new Error('source-unavailable');
        const length = response.headers.get('Content-Length');
        if (length !== null && (!/^\d+$/.test(length) || Number(length) > maximumBytes)) throw new Error('source-size-invalid');
        reader = response.body.getReader();
        let total = 0;
        while (true) {
            const item = await reader.read();
            if (item.done) break;
            chunks.push(item.value);
            total += item.value.byteLength;
            if (total > maximumBytes) throw new Error('source-size-invalid');
        }
        const result = new Uint8Array(total);
        let offset = 0;
        for (const chunk of chunks) { result.set(chunk, offset); offset += chunk.byteLength; }
        return result;
    } catch {
        throw new Error('source-unavailable');
    } finally {
        clearTimeout(timeout);
        cancellation.abort();
        if (reader) { try { await reader.cancel(); } catch { } reader.releaseLock(); }
        for (const chunk of chunks) chunk.fill(0);
        if (currentRequest === cancellation) currentRequest = undefined;
    }
}
