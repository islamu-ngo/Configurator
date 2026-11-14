function clearOutput(state) {
    state.output?.fill(0);
    state.disclosure?.fill(0);
    state.output = null;
    state.disclosure = null;
    state.outputRequest = null;
    state.outputKind = null;
}

export function createBridge() {
    let owner = null;

    function retire(channel) {
        if (!owner || owner.channel !== channel) return;
        const state = owner;
        owner = null;
        state.worker.onmessage = null;
        state.worker.onerror = null;
        state.worker.onmessageerror = null;
        state.worker.terminate();
        clearOutput(state);
        state.rejectReady?.(new Error("profile-worker-retired"));
        state.pending?.reject(new Error("profile-worker-retired"));
        state.pending = null;
    }

    async function open(channel) {
        if (owner) {
            if (owner.channel !== channel) throw new Error("profile-worker-busy");
            return owner.ready;
        }
        const worker = new Worker(new URL("../profile-worker/profile-worker.js", import.meta.url), { type: "module" });
        const state = {
            channel, worker, ready: null, rejectReady: null, pending: null,
            output: null, outputRequest: null, outputKind: null, disclosure: null
        };
        owner = state;
        state.ready = new Promise((resolve, reject) => {
            state.rejectReady = reject;
            worker.onmessage = ({ data }) => {
                if (owner !== state || data?.channel !== channel) {
                    data?.output?.fill?.(0);
                    return;
                }
                if (data.kind === "ready") {
                    state.rejectReady = null;
                    resolve();
                    return;
                }
                if (data.kind !== "reply" || !state.pending || typeof data.reply !== "string" || data.reply.length > 4194304) {
                    data?.output?.fill?.(0);
                    retire(channel);
                    return;
                }
                const pending = state.pending;
                try {
                    const reply = JSON.parse(data.reply);
                    const request = pending.request;
                    if (reply.requestId !== request.requestId ||
                        JSON.stringify(reply.authority) !== JSON.stringify(request.authority))
                        throw new Error("profile-worker-stale");
                    const allowed = request.operation === "inspect" ? "inspect" :
                        request.operation === "download" ? "artifact" :
                            ["seal", "create"].includes(request.operation) ? "profile" : null;
                    if (data.output !== null && (!(data.output instanceof Uint8Array) ||
                        data.output.length > 4194304 || allowed === null || reply.outputKind !== allowed || reply.status !== "ok"))
                        throw new Error("profile-worker-output");
                    state.output = data.output;
                    state.outputRequest = request.requestId;
                    state.outputKind = data.output === null ? null : reply.outputKind;
                    state.pending = null;
                    pending.resolve(data.reply);
                } catch {
                    data?.output?.fill?.(0);
                    retire(channel);
                }
            };
            worker.onerror = event => { event.preventDefault(); retire(channel); };
            worker.onmessageerror = () => retire(channel);
            worker.postMessage({ kind: "boot", channel });
        });
        return state.ready;
    }

    function execute(channel, metadata, payload, credential, definition, answers, recipients) {
        const incoming = [payload, credential, definition, answers, ...recipients];
        try {
            const state = owner;
            if (!state || state.channel !== channel || state.pending || state.output !== null || state.disclosure !== null)
                throw new Error("profile-worker-busy");
            if (typeof metadata !== "string" || metadata.length > 262144) throw new Error("profile-worker-input");
            const request = JSON.parse(metadata);
            if (request.authority?.consentGranted !== true) throw new Error("profile-worker-consent");
            const limits = [4194304, 1024, 2097152, 2097152, ...recipients.map(() => 1024)];
            if (recipients.length > 8 || incoming.some((bytes, index) =>
                !(bytes instanceof Uint8Array) || bytes.length > limits[index]))
                throw new Error("profile-worker-input");
            const copies = incoming.map(bytes => bytes.slice());
            return new Promise((resolve, reject) => {
                state.pending = { request, resolve, reject };
                try {
                    state.worker.postMessage({
                        kind: "execute", channel, metadata,
                        payload: copies[0], credential: copies[1], definition: copies[2], answers: copies[3],
                        recipients: copies.slice(4)
                    }, copies.map(bytes => bytes.buffer));
                } catch {
                    copies.forEach(bytes => { if (bytes.byteLength) bytes.fill(0); });
                    retire(channel);
                }
            });
        } finally {
            incoming.forEach(bytes => { if (bytes instanceof Uint8Array) bytes.fill(0); });
        }
    }

    function discard(channel) {
        if (owner?.channel === channel) clearOutput(owner);
    }

    function download(channel, requestId, kind) {
        const state = owner;
        if (!state || state.channel !== channel || state.outputRequest !== requestId ||
            !state.output || state.outputKind !== kind || !["artifact", "profile"].includes(kind))
            throw new Error("profile-worker-output");
        const url = URL.createObjectURL(new Blob([state.output], { type: "application/octet-stream" }));
        try {
            const anchor = document.createElement("a");
            anchor.href = url;
            anchor.download = kind === "profile" ? "configuration.cfgvault" : "configuration-artifact";
            document.body.append(anchor);
            try { anchor.click(); }
            finally { anchor.remove(); }
        } finally {
            clearOutput(state);
            URL.revokeObjectURL(url);
        }
    }

    function takeDisclosure(channel, requestId) {
        const state = owner;
        if (!state || state.channel !== channel || state.outputRequest !== requestId ||
            state.outputKind !== "inspect" || !state.output)
            throw new Error("profile-worker-output");
        const result = state.output.slice();
        clearOutput(state);
        state.disclosure = result;
        return result;
    }

    return { open, execute, retire, discard, download, takeDisclosure };
}
