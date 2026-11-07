import { createEditor } from "../vendor/configuration-editor.js";

// Each component owns this single slot. There is no global document registry.
export function createBridge() {
  let active = null;
  let fence = 0;

  function bound(owned) {
    return owned.host?.isConnected && owned.consent?.isConnected &&
      owned.consent.checked && owned.consent.dataset.workspaceLifetime === owned.workspaceLifetime &&
      owned.host.dataset.editorLifetime === owned.editorLifetime;
  }

  function current(token, version) {
    const owned = active;
    if (owned && !bound(owned)) {
      retire(owned.token);
      throw new Error("developer-stale");
    }
    if (!owned || token !== owned.token || token <= fence ||
        (version !== undefined && !owned.editor.isCurrent(version)))
      throw new Error("developer-stale");
    return owned;
  }

  function clearRead(owned) {
    owned?.transfer?.fill(0);
    if (owned) owned.transfer = null;
  }

  // Coordinate-only private map: never decode or retain source strings here.
  function coordinates(bytes) {
    const starts = [0];
    const ends = [];
    let length = 0;
    for (const byte of bytes) {
      if (byte === 10) {
        ends.push(length);
        starts.push(++length);
      } else if ((byte & 0xc0) !== 0x80) {
        length += byte >= 0xf0 ? 2 : 1;
      }
    }
    ends.push(length);
    return { starts, ends, length };
  }

  function retire(token) {
    fence = Math.max(fence, token);
    const owned = active;
    if (!owned || owned.token > token) return;
    active = null;
    owned.consent?.removeEventListener("change", owned.revoke, true);
    globalThis.removeEventListener("configurator-custody-abandoned", owned.abandon);
    clearRead(owned);
    owned.locations = null;
    owned.callback = null;
    owned.consent = null;
    try { owned.editor.dispose(); }
    finally {
      owned.editor = null;
      owned.host.replaceChildren();
      owned.host = null;
    }
  }

  return {
    mount(token, host, callback, options) {
      const source = options.source;
      try {
        if (token <= fence || active) throw new Error("developer-stale");
        const consent = options.consent;
        const owned = { token, host, callback, consent, workspaceLifetime: options.workspaceLifetime,
          editorLifetime: options.editorLifetime, editor: null, transfer: null, locations: null, suppress: false, historyLimited: false };
        if (!(consent instanceof HTMLInputElement) || consent.type !== "checkbox" ||
            !owned.workspaceLifetime || !owned.editorLifetime || !bound(owned))
          throw new Error("developer-consent-required");
        owned.revoke = () => {
          if (!consent.checked) retire(token);
        };
        owned.abandon = () => retire(token);
        owned.editor = createEditor(host, {
          ...options,
          onChange: metadata => {
            if (active !== owned || owned.suppress || token <= fence) return;
            if (!bound(owned)) { retire(token); return; }
            owned.historyLimited ||= metadata.historyLimited === true;
            clearRead(owned);
            owned.locations = null;
            owned.callback?.invokeMethodAsync("OnEditorChangedAsync", token, metadata.version,
              metadata.dirty === true, owned.historyLimited).catch(() => retire(token));
          }
        });
        active = owned;
        consent.addEventListener("change", owned.revoke, true);
        globalThis.addEventListener("configurator-custody-abandoned", owned.abandon);
        return owned.editor.version;
      } finally {
        source?.fill(0);
        options.source = null;
      }
    },
    read(token, version) {
      const owned = current(token, version);
      clearRead(owned);
      owned.transfer = owned.editor.read(version);
      owned.locations = coordinates(owned.transfer);
      if (!owned.editor.isCurrent(version)) {
        clearRead(owned);
        throw new Error("developer-stale");
      }
      return owned.transfer;
    },
    releaseRead(token, version) {
      if (active?.token === token && active.editor.isCurrent(version)) clearRead(active);
    },
    freeze(token, version, frozen) {
      const owned = current(token, frozen ? version : undefined);
      owned.editor.setReadOnly(frozen);
      return !frozen || owned.editor.isCurrent(version);
    },
    diagnostics(token, version, list) {
      const owned = current(token, version);
      const map = owned.locations;
      const diagnostics = list.slice(0, 100).map(item => {
        const line = Math.max(1, item.line);
        const column = Math.max(1, item.column);
        const start = map?.starts[line - 1] ?? 0;
        const end = map?.ends[line - 1] ?? 0;
        const from = Math.min(start + column - 1, end);
        return { from, to: Math.min(from + 1, end), severity: "error",
          message: `${item.code} (${line}:${column})` };
      });
      owned.editor.setDiagnostics(diagnostics);
    },
    replace(token, version, bytes) {
      try {
        const owned = current(token, version);
        clearRead(owned);
        owned.locations = null;
        owned.suppress = true;
        try {
          const next = owned.editor.replaceSource(bytes);
          owned.editor.setDiagnostics([]);
          return { version: next, dirty: owned.editor.dirty, historyLimited: owned.historyLimited };
        } finally { owned.suppress = false; }
      } finally { bytes?.fill(0); }
    },
    markApplied(token, version) {
      const owned = current(token, version);
      owned.suppress = true;
      try { owned.editor.markApplied(); return true; }
      finally { owned.suppress = false; }
    },
    metadata(token, label, describedBy, locale, phrases) {
      const owned = current(token);
      owned.editor.setMetadata(label, describedBy, locale, phrases);
      owned.host.lang = locale;
    },
    focus(token, version) { current(token, version).editor.focus(); },
    retire
  };
}
