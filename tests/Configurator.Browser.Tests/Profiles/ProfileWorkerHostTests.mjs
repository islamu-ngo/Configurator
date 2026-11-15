import test from "node:test";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { registerHooks } from "node:module";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";

const moduleUrl = new URL("../../../src/Configurator.Browser/wwwroot/js/profile-worker-host.js", import.meta.url);
// Only the external worker and DOM are substituted. Every admission, fence, transfer and sink is the shipped module.
async function fixture(t) {
    let worker;
    const workers = [];
    let downloads = 0;
    const previousWorker = globalThis.Worker;
    const previousDocument = globalThis.document;
    globalThis.Worker = class {
        terminated = false;
        received = null;
        constructor() { worker = this; workers.push(this); }
        postMessage(message, transfer = []) {
            this.received = structuredClone(message, { transfer });
            if (message.kind === "boot")
                this.onmessage({ data: { kind: "ready", channel: message.channel } });
        }
        terminate() { this.terminated = true; }
    };
    globalThis.document = {
        body: { append() {} },
        createElement() { return { click() { downloads++; }, remove() {} }; }
    };
    t.after(() => {
        globalThis.Worker = previousWorker;
        globalThis.document = previousDocument;
    });
    // The browser loads this exact .js file as ESM. Declare that same format in Node;
    // do not inherit an unrelated ancestor package's CommonJS default or rewrite the source.
    const format = registerHooks({
        load(url, context, nextLoad) {
            return nextLoad(url, url.startsWith(moduleUrl.href) ? { ...context, format: "module" } : context);
        }
    });
    let module;
    try { module = await import(`${moduleUrl.href}?case=${randomUUID()}`); }
    finally { format.deregister(); }
    const host = module.createBridge();
    t.after(() => host.retire("owned"));
    await host.open("owned");
    return { host, worker, workers, createHost: () => module.createBridge(), downloads: () => downloads };
}

function request(operation, requestId = 1) {
    return {
        authority: {
            workspaceId: "cc19d015-c482-4f3d-93da-8d5534eddede",
            generation: 1, answerRevision: 0, consentEpoch: 1, consentGranted: true
        },
        operation, requestId
    };
}

function execute(host, command, payload = new Uint8Array()) {
    return host.execute("owned", JSON.stringify(command), payload,
        new Uint8Array(), new Uint8Array(), new Uint8Array(), []);
}

function deliver(worker, command, outputKind = null, channel = "owned") {
    const output = outputKind === null ? null : new TextEncoder().encode("synthetic-restricted-output");
    worker.onmessage({ data: {
        kind: "reply", channel, output,
        reply: JSON.stringify({
            authority: command.authority, requestId: command.requestId, status: "ok", outputKind
        })
    } });
    return output;
}

for (const refused of ["consent", "channel"]) {
    test(`refused ${refused} clears the supplied interop copies without transferring them`, async t => {
        const { host, worker } = await fixture(t);
        const command = request("unlock");
        if (refused === "consent") command.authority.consentGranted = false;
        const payload = new TextEncoder().encode("synthetic-refused-profile");
        assert.throws(() => host.execute(refused === "channel" ? "other" : "owned", JSON.stringify(command),
            payload, new Uint8Array(), new Uint8Array(), new Uint8Array(), []));
        assert.equal(worker.received.kind, "boot");
        assert.ok(payload.every(value => value === 0));
    });
}

test("one owned worker rejects other owners and queued operations, then retires synchronously", async t => {
    const { host, worker } = await fixture(t);
    await assert.rejects(host.open("other"), /profile-worker-busy/);
    const payload = new TextEncoder().encode("synthetic-profile-input");
    const command = request("unlock");
    const pending = execute(host, command, payload);
    assert.ok(payload.every(value => value === 0));
    assert.equal(new TextDecoder().decode(worker.received.payload), "synthetic-profile-input");
    assert.throws(() => execute(host, request("unlock", 2)), /profile-worker-busy/);
    const reject = assert.rejects(pending, /profile-worker-retired/);
    const oldListener = worker.onmessage;
    host.retire("owned");
    assert.equal(worker.terminated, true);
    await reject;
    const late = new TextEncoder().encode("synthetic-late-secret");
    oldListener({ data: { kind: "reply", channel: "owned", output: late } });
    assert.ok(late.every(value => value === 0));
});

test("independent recovery custody survives retirement of the primary bridge", async t => {
    const { host, worker, workers, createHost } = await fixture(t);
    const recovery = createHost();
    t.after(() => recovery.retire("recovery"));
    await recovery.open("recovery");
    const recoveryWorker = workers[1];
    assert.equal(workers.length, 2);
    assert.notEqual(recoveryWorker, worker);
    await assert.rejects(host.open("recovery"), /profile-worker-busy/);
    await assert.rejects(recovery.open("owned"), /profile-worker-busy/);
    const command = request("unlock");
    const primaryPending = execute(host, command);
    const recoveryExecute = () => recovery.execute("recovery", JSON.stringify(request("recover")),
        new Uint8Array(), new Uint8Array(), new Uint8Array(), new Uint8Array(), []);
    const recoveryPending = recoveryExecute();
    assert.throws(() => execute(host, request("unlock", 2)), /profile-worker-busy/);
    assert.throws(recoveryExecute, /profile-worker-busy/);
    const retired = assert.rejects(primaryPending, /profile-worker-retired/);
    host.retire("owned");
    assert.equal(worker.terminated, true);
    assert.equal(recoveryWorker.terminated, false);
    await retired;
    deliver(recoveryWorker, request("recover"), null, "recovery");
    assert.equal(JSON.parse(await recoveryPending).status, "ok");
});

test("artifact bytes cannot be taken through the inspectable disclosure channel", async t => {
    const { host, worker } = await fixture(t);
    const command = request("download");
    const pending = execute(host, command);
    deliver(worker, command, "artifact");
    const reply = await pending;
    assert.equal(reply.includes("synthetic-restricted-output"), false);
    assert.throws(() => host.takeDisclosure("owned", command.requestId), /profile-worker-output/);
});

test("inspectable bytes cannot be relabelled as an artifact download", async t => {
    const { host, worker, downloads } = await fixture(t);
    const command = request("inspect");
    const pending = execute(host, command);
    deliver(worker, command, "inspect");
    await pending;
    assert.throws(() => host.download("owned", command.requestId, "artifact"), /profile-worker-output/);
    assert.equal(downloads(), 0);
});

test("the inspection interop copy remains clearable until the managed caller discards it", async t => {
    const { host, worker } = await fixture(t);
    const command = request("inspect");
    const pending = execute(host, command);
    const owned = deliver(worker, command, "inspect");
    await pending;
    const transferred = host.takeDisclosure("owned", command.requestId);
    assert.equal(new TextDecoder().decode(transferred), "synthetic-restricted-output");
    assert.ok(owned.every(value => value === 0));
    host.discard("owned");
    assert.ok(transferred.every(value => value === 0));
});

test("deliberate artifact download consumes its bounded owned output once", async t => {
    const { host, worker, downloads } = await fixture(t);
    const command = request("download");
    const pending = execute(host, command);
    const owned = deliver(worker, command, "artifact");
    await pending;
    host.download("owned", command.requestId, "artifact");
    assert.equal(downloads(), 1);
    assert.ok(owned.every(value => value === 0));
    assert.throws(() => host.download("owned", command.requestId, "artifact"), /profile-worker-output/);
});

for (const basePath of ["/", "/configurator/"]) {
    test(`published profile runtime is prepared and cache-only during custody at ${basePath}`, async () => {
        const scope = new URL(basePath, "https://configurator.invalid");
        const runtimePaths = [
            "js/profile-worker-host.js", "profile-worker/profile-worker.js",
            "profile-worker/_framework/dotnet.js", "profile-worker/_framework/dotnet.native.wasm",
            "profile-worker/_framework/Configurator.Browser.Worker.wasm",
            "profile-worker/vendor/profile-crypto.js",
            "profile-worker/vendor/profile-crypto.manifest.json",
            "profile-worker/vendor/profile-crypto.LICENSE.txt"
        ];
        const excludedPaths = ["profiles/private.cfgvault", "artifacts/target.json", "profile-worker-private.js"];
        const manifestPaths = ["index.html", "_framework/dotnet.js", ...runtimePaths, ...excludedPaths];
        const entries = new Map();
        const listeners = new Map();
        let installedRequests;
        const cache = {
            async addAll(requests) {
                installedRequests = requests;
                for (const request of requests) entries.set(request.url, new Response(request.url));
            },
            async match(url) { return entries.get(url); }
        };
        const source = await readFile(new URL(
            "../../../src/Configurator.Browser/wwwroot/service-worker.published.js", import.meta.url), "utf8");
        runInNewContext(source, {
            URL, Request, Response, importScripts() {},
            caches: { async has() { return false; }, async open() { return cache; } },
            fetch() { assert.fail("profile runtime must never fall back to network"); },
            self: {
                registration: { scope: scope.href }, navigator: { onLine: true },
                assetsManifest: {
                    version: "synthetic-profile-runtime",
                    assets: manifestPaths.map(url => ({ url, hash: "sha256-c3ludGhldGlj" }))
                },
                addEventListener(type, listener) { listeners.set(type, listener); }
            }
        });
        await new Promise((resolve, reject) => listeners.get("install")({
            waitUntil(pending) { pending.then(resolve, reject); }
        }));
        async function message(data) {
            let reply;
            await new Promise((resolve, reject) => listeners.get("message")({
                source: { id: "workspace" }, data,
                ports: [{ postMessage(value) { reply = value; } }],
                waitUntil(pending) { pending.then(resolve, reject); }
            }));
            return reply;
        }
        const status = await message({ type: "status" });
        assert.equal(status.appPrepared, true);
        for (const path of runtimePaths) {
            const url = new URL(path, scope).href;
            assert.ok(status.assets.includes(url), `${path} must reach the page guard before custody`);
            const installed = installedRequests.find(request => request.url === url);
            assert.equal(installed.integrity, "sha256-c3ludGhldGlj");
            assert.equal(installed.credentials, "omit");
            assert.equal(installed.cache, "reload");
        }
        await message({ type: "custody", active: true });
        for (const path of [...runtimePaths, ...excludedPaths, "profile-worker/not-in-manifest.js"]) {
            const url = new URL(path, scope).href;
            const response = await new Promise(resolve => listeners.get("fetch")({
                request: new Request(url), clientId: "workspace", respondWith: resolve
            }));
            if (runtimePaths.includes(path)) assert.equal(await response.text(), url);
            else assert.equal(response.type, "error");
        }
        entries.delete(new URL("profile-worker/_framework/dotnet.native.wasm", scope).href);
        const incomplete = await message({ type: "status" });
        assert.equal(incomplete.appPrepared, false);
        assert.equal(incomplete.offlineReady, false);
        const missing = await new Promise(resolve => listeners.get("fetch")({
            request: new Request(new URL("profile-worker/_framework/dotnet.native.wasm", scope)),
            clientId: "workspace", respondWith: resolve
        }));
        assert.equal(missing.type, "error");
    });
}

test("published service worker never invokes skipWaiting while existing client stays active", async () => {
    const scope = new URL("/", "https://configurator.invalid");
    const source = await readFile(new URL(
        "../../../src/Configurator.Browser/wwwroot/service-worker.published.js", import.meta.url), "utf8");
    let skipWaitingCalled = false;
    let claimedClients = false;
    const listeners = new Map();
    const deletedCaches = [];
    runInNewContext(source, {
        URL, Request, Response, importScripts() {},
        caches: {
            async has() { return true; },
            async open() { return { async addAll() {}, async match() { return undefined; } }; },
            async keys() { return ["configurator-app-v1", "configurator-app-v2"]; },
            async delete(name) { deletedCaches.push(name); return true; }
        },
        self: {
            registration: { scope: scope.href },
            navigator: { onLine: true },
            assetsManifest: { version: "v2", assets: [] },
            skipWaiting() { skipWaitingCalled = true; return Promise.resolve(); },
            clients: { async claim() { claimedClients = true; } },
            addEventListener(type, listener) { listeners.set(type, listener); }
        }
    });
    await new Promise((resolve, reject) => listeners.get("install")({
        waitUntil(pending) { pending.then(resolve, reject); }
    }));
    assert.equal(skipWaitingCalled, false);
    assert.equal(claimedClients, false);
    await new Promise((resolve, reject) => listeners.get("activate")({
        waitUntil(pending) { pending.then(resolve, reject); }
    }));
    assert.equal(skipWaitingCalled, false);
    assert.deepEqual(deletedCaches, ["configurator-app-v1"]);
});

test("pwa-lifecycle tracks waiting updates without reloading or replacing active workspace", async () => {
    const source = await readFile(new URL(
        "../../../src/Configurator.Browser/wwwroot/pwa-lifecycle.js", import.meta.url), "utf8");
    const waitingWorkerMessages = [];
    let reloaded = false;
    const activeWorker = {
        state: "activated",
        postMessage(data, ports) {
            if (data?.type === "status") {
                ports?.[0]?.postMessage({ appPrepared: true, offlineReady: true, assets: [] });
            } else if (data?.type === "custody") {
                ports?.[0]?.postMessage({ accepted: true });
            }
        }
    };
    const waitingWorker = {
        state: "installed",
        postMessage(data) { waitingWorkerMessages.push(data); }
    };
    const registrationListeners = new Map();
    const mockRegistration = {
        active: activeWorker,
        waiting: waitingWorker,
        installing: null,
        addEventListener(type, listener) { registrationListeners.set(type, listener); }
    };
    const windowStub = {
        fetch: async () => new Response("ok"),
        WebSocket: class {},
        EventSource: class {},
        addEventListener() {},
        removeEventListener() {},
        dispatchEvent() {}
    };
    const documentStub = {
        baseURI: "https://configurator.invalid/",
        getElementById() { return null; },
        querySelectorAll() { return []; }
    };
    const sandbox = {
        URL, Request, Response, CustomEvent, MessageChannel, setTimeout, clearTimeout,
        XMLHttpRequest: class { open() {} },
        WebSocket: class {},
        EventSource: class {},
        window: windowStub,
        document: documentStub,
        location: { origin: "https://configurator.invalid", reload() { reloaded = true; } },
        navigator: {
            onLine: true,
            sendBeacon: () => true,
            serviceWorker: {
                controller: activeWorker,
                register: async () => mockRegistration
            }
        }
    };
    const stripped = source.replace(/^export\s+/gm, "");
    const api = {};
    runInNewContext(`${stripped}\nObject.assign(__exports, { initialize, beginWorkspace, endWorkspace, canFetchDefinition });`, {
        ...sandbox,
        __exports: api
    });
    const state = await api.initialize();
    assert.equal(state.appPrepared, true);
    assert.equal(state.updateWaiting, true);
    assert.equal(windowStub.configuratorPwa.hasWaitingUpdate(), true);
    assert.equal(await api.beginWorkspace(1), true);
    const controllerUpdate = registrationListeners.get("updatefound");
    if (controllerUpdate) controllerUpdate();
    assert.equal(reloaded, false);
    assert.equal(waitingWorkerMessages.length, 0);
    assert.equal(api.canFetchDefinition(), false);
    await api.endWorkspace(2);
});
