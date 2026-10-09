const definitionCacheName = 'configurator-public-definitions-v1';
const digestPattern = /^[a-f0-9]{64}$/;
const nativeFetch = window.fetch.bind(window);
let workspaceActive = false;
let initialization;
let registration;
let appPrepared = false;
let offlineReady = false;
let generation = 0;
const appAssets = new Set();
let abandoned = false;

export function canFetchDefinition() {
    return !abandoned && navigator.onLine && !workspaceActive;
}

window.configuratorPwa = Object.freeze({ canFetchDefinition, prepareDefinitionFetch, isAbandoned: () => abandoned });

window.addEventListener('pagehide', () => {
    abandoned = true;
    const app = document.getElementById('app');
    if (app) { app.hidden = true; app.inert = true; }
    for (const control of document.querySelectorAll('input, textarea, select')) {
        if (control instanceof HTMLSelectElement) control.selectedIndex = -1;
        else {
            control.value = '';
            control.removeAttribute('value');
            if (control instanceof HTMLInputElement) { control.checked = false; control.removeAttribute('checked'); }
            if (control instanceof HTMLTextAreaElement) control.textContent = '';
        }
    }
    for (const preview of document.querySelectorAll('pre, output, [contenteditable]')) preview.textContent = '';
    for (const link of document.querySelectorAll('a[href^="blob:"], a[href^="data:"]')) link.removeAttribute('href');
    window.dispatchEvent(new CustomEvent('configurator-custody-abandoned'));
    (navigator.serviceWorker?.controller ?? registration?.active)?.postMessage({ type: 'custody', active: false });
});

window.addEventListener('pageshow', event => {
    // History restoration starts a clean realm; an abandoned private workspace is never restored from BFCache.
    if (event.persisted || abandoned) {
        const app = document.getElementById('app');
        if (app) { app.hidden = true; app.inert = true; }
        location.reload();
    }
});

export async function prepareDefinitionFetch(url) {
    if (!canFetchDefinition()) return false;
    const worker = navigator.serviceWorker?.controller;
    if (!worker) return true;
    return await new Promise(resolve => {
        const channel = new MessageChannel();
        const timeout = setTimeout(() => { channel.port1.close(); resolve(false); }, 15000);
        channel.port1.onmessage = event => {
            clearTimeout(timeout);
            channel.port1.close();
            resolve(event.data?.accepted === true && canFetchDefinition());
        };
        worker.postMessage({ type: 'definition-source', url }, [channel.port2]);
    });
}

// Block page connection APIs during custody; the worker serves immutable app assets from its cache.
window.fetch = (input, options) => {
    if (abandoned) return Promise.reject(new TypeError('This workspace has been abandoned.'));
    if (!canFetchDefinition()) {
        const request = new Request(input, options);
        const url = new URL(request.url);
        const cachedBootAsset = !workspaceActive &&
            url.pathname.startsWith(new URL('_framework/', document.baseURI).pathname);
        if (request.method !== 'GET' || url.origin !== location.origin || url.search ||
            !(appAssets.has(url.href) || cachedBootAsset)) {
            return Promise.reject(new TypeError('Application connections are unavailable.'));
        }
        if (!navigator.serviceWorker?.controller) {
            return Promise.reject(new TypeError('Application assets are unavailable offline.'));
        }
    }
    return nativeFetch(input, options);
};

const nativeOpen = XMLHttpRequest.prototype.open;
XMLHttpRequest.prototype.open = function (...args) {
    if (!canFetchDefinition()) throw new TypeError('Application connections are unavailable.');
    return nativeOpen.apply(this, args);
};
for (const name of ['WebSocket', 'EventSource']) {
    const Native = window[name];
    window[name] = class extends Native {
        constructor(...args) {
            if (!canFetchDefinition()) throw new TypeError('Application connections are unavailable.');
            super(...args);
        }
    };
}
const nativeBeacon = navigator.sendBeacon.bind(navigator);
navigator.sendBeacon = (...args) => canFetchDefinition() && nativeBeacon(...args);

function workerRequest(worker, type, active = false) {
    if (!worker) return Promise.resolve(null);
    return new Promise(resolve => {
        const channel = new MessageChannel();
        const timeout = setTimeout(() => { channel.port1.close(); resolve(null); }, 15000);
        channel.port1.onmessage = event => {
            clearTimeout(timeout);
            channel.port1.close();
            resolve(event.data);
        };
        worker.postMessage({ type, active }, [channel.port2]);
    });
}

async function initializeApp() {
    if (!('serviceWorker' in navigator)) return { appPrepared: false, offlineReady: false, online: navigator.onLine };
    try {
        registration = await navigator.serviceWorker.register(new URL('service-worker.js', document.baseURI),
            { scope: new URL('./', document.baseURI).pathname, updateViaCache: 'none' });
        const worker = registration.active ?? registration.installing;
        if (worker && worker.state !== 'activated') {
            await new Promise((resolve, reject) => {
                const timeout = setTimeout(() => { worker.removeEventListener('statechange', changed); reject(new Error()); }, 30000);
                function changed() {
                    if (worker.state === 'activated' || worker.state === 'redundant') {
                        clearTimeout(timeout);
                        worker.removeEventListener('statechange', changed);
                        if (worker.state === 'activated') resolve(); else reject(new Error());
                    }
                }
                worker.addEventListener('statechange', changed);
                changed();
            });
        }
        const state = await workerRequest(registration.active, 'status');
        appPrepared = state?.appPrepared === true;
        offlineReady = state?.offlineReady === true;
        if (appPrepared) for (const url of state.assets ?? []) appAssets.add(url);
    } catch {
        appPrepared = false;
        offlineReady = false;
    }
    return { appPrepared, offlineReady, online: navigator.onLine };
}

export function initialize() {
    initialization ??= initializeApp();
    return initialization;
}

export async function beginWorkspace(operation) {
    if (operation < generation) return false;
    generation = operation;
    await initialize();
    if (abandoned || !appPrepared || operation !== generation) return false;
    workspaceActive = true;
    const state = await workerRequest(navigator.serviceWorker.controller ?? registration?.active, 'custody', true);
    if (operation !== generation) return false;
    if (state?.accepted !== true) { workspaceActive = false; return false; }
    return true;
}

export async function endWorkspace(operation) {
    if (operation < generation) return;
    generation = operation;
    // A clear can race beginWorkspace; the worker receives transitions in their posted order.
    workspaceActive = false;
    await workerRequest(navigator.serviceWorker?.controller ?? registration?.active, 'custody', false);
}

export async function retainDefinition(digest, bytes) {
    if (abandoned || workspaceActive || !offlineReady || !digestPattern.test(digest) || bytes.length > 4194304) return false;
    const actualDigest = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)),
        value => value.toString(16).padStart(2, '0')).join('');
    if (actualDigest !== digest || abandoned || workspaceActive) return false;
    try {
        const cache = await caches.open(definitionCacheName);
        const key = new URL(`_retained-definitions/${digest}`, document.baseURI);
        const keys = await cache.keys();
        if (abandoned || workspaceActive || keys.length >= 8 && !keys.some(item => item.url === key.href)) return false;
        await cache.put(key.href, new Response(bytes, { headers: { 'Content-Type': 'application/json' } }));
        return true;
    } catch { return false; }
}

export async function readDefinition(digest) {
    if (abandoned || !digestPattern.test(digest)) return null;
    try {
        const cache = await caches.open(definitionCacheName);
        const response = await cache.match(new URL(`_retained-definitions/${digest}`, document.baseURI).href);
        if (!response?.body) return null;
        const reader = response.body.getReader();
        const chunks = [];
        let length = 0;
        try {
            while (true) {
                const item = await reader.read();
                if (item.done) break;
                length += item.value.length;
                if (length > 4194304) { await reader.cancel(); return null; }
                chunks.push(item.value);
            }
        } finally { reader.releaseLock(); }
        const bytes = new Uint8Array(length);
        let offset = 0;
        for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
        return bytes;
    } catch { return null; }
}

export async function retainedDigests() {
    if (abandoned) return [];
    try {
        const cache = await caches.open(definitionCacheName);
        const prefix = new URL('_retained-definitions/', document.baseURI).href;
        return (await cache.keys()).map(item => item.url.startsWith(prefix) ? item.url.slice(prefix.length) : '')
            .filter(item => digestPattern.test(item)).slice(0, 8);
    } catch { return []; }
}
