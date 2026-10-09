const sessions = new Map();

function clear(session) {
    session.bytes?.fill(0);
    if (session.url) URL.revokeObjectURL(session.url);
    if (session.preview) session.preview.textContent = "";
    session.bytes = null;
    session.url = null;
    session.preview = null;
}

window.addEventListener("configurator-custody-abandoned", () => {
    for (const session of sessions.values()) {
        clear(session);
        session.generation = Number.MAX_SAFE_INTEGER;
    }
});

export function invalidate(id, generation) {
    const session = sessions.get(id) ?? { generation: 0 };
    if (generation < session.generation) return;
    clear(session);
    session.generation = generation;
    sessions.set(id, session);
}

export function open(id, generation, bytes) {
    if (document.getElementById("app")?.inert) {
        bytes.fill?.(0);
        return;
    }
    const session = sessions.get(id) ?? { generation: 0 };
    if (generation < session.generation) return;
    if (generation === session.generation && session.bytes) return;
    clear(session);
    session.generation = generation;
    session.bytes = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
    sessions.set(id, session);
}

function current(id, generation) {
    if (document.getElementById("app")?.inert) return null;
    const session = sessions.get(id);
    return session?.generation === generation && session.bytes ? session : null;
}

export async function copy(id, generation) {
    const session = current(id, generation);
    if (!session || !navigator.clipboard?.writeText) return false;
    try {
        await navigator.clipboard.writeText(new TextDecoder().decode(session.bytes));
        return current(id, generation) === session;
    } catch { return false; }
}

export function reveal(id, generation, element) {
    const session = current(id, generation);
    if (!session || !element?.isConnected) return false;
    element.textContent = new TextDecoder().decode(session.bytes);
    session.preview = element;
    return true;
}

export function download(id, generation, fileName, mediaType) {
    const session = current(id, generation);
    if (!session) return false;
    session.url = URL.createObjectURL(new Blob([session.bytes], { type: mediaType }));
    const link = document.createElement("a");
    link.href = session.url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(session.url);
    session.url = null;
    return true;
}
