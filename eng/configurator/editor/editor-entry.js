import { Compartment, EditorSelection, EditorState } from "@codemirror/state";
import { drawSelection, EditorView, keymap, lineNumbers } from "@codemirror/view";
import { defaultKeymap } from "@codemirror/commands";
import { defaultHighlightStyle, HighlightStyle, StreamLanguage, syntaxHighlighting } from "@codemirror/language";
import { json } from "@codemirror/lang-json";
import { autocompletion, completionKeymap } from "@codemirror/autocomplete";
import { closeSearchPanel, openSearchPanel, search, searchKeymap, searchPanelOpen } from "@codemirror/search";
import { lintGutter, setDiagnostics as diagnosticTransaction } from "@codemirror/lint";

const retainedTextLimit = 4 * 1024 * 1024;
const groupLimit = 100;
const diagnosticLimit = 100;
const themedHighlightStyle = HighlightStyle.define(defaultHighlightStyle.specs.map(style =>
  style.color ? {
    ...style,
    color: `light-dark(${style.color}, color-mix(in srgb, ${style.color} 35%, var(--isl-color-text)))`
  } : style));

// Counts logical UTF-8 text without allocating an additional answer byte buffer.
// Lone surrogates cannot make a lossless UTF-8 round trip.
function utf8Length(text) {
  let length = 0;
  for (let index = 0; index < text.length; index++) {
    const code = text.charCodeAt(index);
    if (code < 0x80) length++;
    else if (code < 0x800) length += 2;
    else if (code >= 0xd800 && code <= 0xdbff) {
      const next = text.charCodeAt(++index);
      if (!(next >= 0xdc00 && next <= 0xdfff)) return Infinity;
      length += 4;
    } else if (code >= 0xdc00 && code <= 0xdfff) return Infinity;
    else length += 3;
  }
  return length;
}

function documentBytes(doc) {
  let bytes = 0;
  const iterator = doc.iter();
  while (!iterator.next().done) bytes += utf8Length(iterator.value);
  return bytes;
}

function consumeSource(bytes) {
  if (!(bytes instanceof Uint8Array)) throw new TypeError("Editor source must be UTF-8 bytes.");
  try {
    if (bytes.byteLength > retainedTextLimit) throw new RangeError("Editor source exceeds its text limit.");
    // Preserve BOM and carriage returns: normalization/formatting belongs to the
    // explicit caller operation, never to mounting or reading an editor.
    return new TextDecoder("utf-8", { fatal: true, ignoreBOM: true }).decode(bytes);
  } finally {
    bytes.fill(0);
  }
}

function publicText(value, maximum, required = false) {
  if (typeof value !== "string" || value.length > maximum || (required && !value.trim())) {
    throw new TypeError("Invalid public editor metadata.");
  }
  return value;
}

function publicPhrases(value) {
  const supplied = value ?? {};
  if (!supplied || typeof supplied !== "object" || Array.isArray(supplied)
    || Object.keys(supplied).length > 256) throw new TypeError("Invalid editor phrases.");
  const result = {};
  for (const [key, text] of Object.entries(supplied)) {
    publicText(key, 256, true);
    result[key] = publicText(text, 2048);
  }
  result.historyLimited ??= "Editor history is limited. An edit that exceeds the text budget is rejected.";
  result.invalidJson ??= "Invalid JSON syntax. Correct the document before applying.";
  result.invalidEnvironment ??= "Invalid Environment syntax. Use an uppercase key and a supported single-line value.";
  return result;
}

// Lexical assistance only. Core still owns bounds, scope, duplicates and admission.
function environmentLine(text, firstLine = false, hasNewline = false) {
  const offset = firstLine && text.startsWith("\ufeff") ? 1 : 0;
  if (offset) text = text.slice(offset);
  if (hasNewline && text.endsWith("\r")) text = text.slice(0, -1);
  if (/[\u0000-\u001f]/u.test(text)) return { invalid: true };
  if (!text.length || text.startsWith("#")) return { comment: text.length > 0 };
  const equals = text.indexOf("=");
  if (equals < 1 || equals > 128 || !/^[A-Z][A-Z0-9_]*$/u.test(text.slice(0, equals))) {
    return { invalid: true };
  }
  const value = text.slice(equals + 1);
  if (/[$`;|&<>]/u.test(value)) return { invalid: true };
  const valid = value.startsWith('"')
    ? /^"(?:[^"\\]|\\["\\])*"$/u.test(value)
    : /^[A-Za-z0-9_./:,@+\-]*$/u.test(value);
  return valid ? { equals: equals + offset } : { invalid: true };
}

const environmentLanguage = StreamLanguage.define({
  startState: () => ({ firstLine: true }),
  blankLine(state) { state.firstLine = false; },
  token(stream, state) {
    // The stream has no final-line indicator; local diagnostics enforce bare CR.
    const line = environmentLine(stream.string, state.firstLine, true);
    if (line.invalid || line.comment) {
      stream.skipToEnd();
      state.firstLine = false;
      return line.invalid ? "invalid" : "comment";
    }
    if (state.firstLine && stream.pos === 0 && stream.peek() === "\ufeff") {
      stream.next();
      if (stream.eol()) state.firstLine = false;
      return null;
    }
    if (stream.pos < line.equals) {
      stream.pos = line.equals;
      return "variableName";
    }
    if (stream.pos === line.equals) {
      stream.next();
      if (stream.eol()) state.firstLine = false;
      return "operator";
    }
    stream.skipToEnd();
    state.firstLine = false;
    return "string";
  }
});

/**
 * Owns one transient pending document, not accepted form state.
 * Input Uint8Arrays are consumed and cleared, including on decoding failure.
 * read() transfers a fresh byte array; its recipient must clear it after use.
 * Disposal retires references, not physical JavaScript string memory.
 */
export function createEditor(element, options) {
  let initialText = "";
  let view = null;
  let status = null;
  let onChange = null;
  let completions = [];
  let phrases = {};
  let history = [];
  let historyPosition = 0;
  let modelVersion = 1;
  let identity = 1;
  let nextIdentity = 1;
  let appliedIdentity = 1;
  let historyLimited = false;
  let readOnly = false;
  let disposed = false;
  let notifying = false;
  const readOnlyCompartment = new Compartment();
  const metadataCompartment = new Compartment();

  function metadataExtensions(label, describedBy, locale) {
    return [
      EditorState.phrases.of(phrases),
      EditorView.contentAttributes.of({
        "aria-label": label,
        ...(describedBy ? { "aria-describedby": describedBy } : {}),
        dir: "ltr",
        lang: locale,
        spellcheck: "false",
        autocapitalize: "off",
        autocomplete: "off"
      })
    ];
  }

  function requireLive() {
    if (disposed) throw new Error("Editor has been disposed.");
  }

  function requireMutable() {
    requireLive();
    if (notifying) throw new Error("Editor mutation during its change callback is not supported.");
  }

  function report() {
    if (disposed) return;
    status.textContent = historyLimited ? phrases.historyLimited : "";
    if (onChange) {
      notifying = true;
      try {
        onChange({ version: modelVersion, dirty: identity !== appliedIdentity, historyLimited });
      } finally {
        notifying = false;
      }
    }
  }

  function rejectLimit() {
    historyLimited = true;
    report();
    return false;
  }

  function localDiagnostics(state) {
    if (representation === "canonical") {
      try {
        JSON.parse(state.doc.toString());
        return [];
      } catch {
        // Engine exception text can contain private input. Never inspect or echo it.
        return [{ from: 0, to: Math.min(1, state.doc.length), severity: "error", message: phrases.invalidJson }];
      }
    }
    const diagnostics = [];
    for (let number = 1; number <= state.doc.lines; number++) {
      const line = state.doc.line(number);
      if (environmentLine(line.text, number === 1, number < state.doc.lines).invalid) {
        diagnostics.push({ from: line.from, to: line.to, severity: "error", message: phrases.invalidEnvironment });
        if (diagnostics.length === diagnosticLimit) break;
      }
    }
    return diagnostics;
  }

  function refreshDiagnostics(extra = []) {
    const local = localDiagnostics(view.state);
    // Caller diagnostics take precedence. The caller owns any truncated-summary UI.
    const diagnostics = extra.concat(local).slice(0, diagnosticLimit);
    view.dispatch(diagnosticTransaction(view.state, diagnostics));
  }

  function inverseGroup(transactions) {
    const steps = [];
    let bytes = 0;
    for (const transaction of transactions) {
      if (!transaction.docChanged) continue;
      const changes = [];
      transaction.changes.iterChanges((fromA, toA, fromB, toB) => {
        const removed = transaction.startState.doc.sliceString(fromA, toA);
        changes.push({ from: fromB, to: toB, insert: removed });
        bytes += utf8Length(removed);
      });
      steps.unshift(changes);
    }
    return {
      steps,
      bytes,
      selection: transactions[0].startState.selection.toJSON(),
      targetIdentity: identity
    };
  }

  // This is the sole document mutation gate, including search/replace, paste,
  // IME, completion, formatting and owned undo/redo. No opaque states are kept.
  function acceptTransactions(transactions, replay = null) {
    requireMutable();
    if (!transactions.length) return true;
    if (transactions[0].startState !== view.state) throw new Error("Stale editor transaction.");
    for (let index = 1; index < transactions.length; index++) {
      if (transactions[index].startState !== transactions[index - 1].state) {
        throw new Error("Non-sequential editor transactions.");
      }
    }
    if (!transactions.some(transaction => transaction.docChanged)) {
      view.update(transactions);
      return true;
    }
    if (readOnly) throw new Error("Editor is read-only.");
    if (modelVersion === Number.MAX_SAFE_INTEGER || nextIdentity === Number.MAX_SAFE_INTEGER) {
      throw new Error("Editor version limit reached.");
    }
    const inverse = inverseGroup(transactions);
    const finalBytes = documentBytes(transactions[transactions.length - 1].newDoc);
    if (finalBytes + inverse.bytes > retainedTextLimit) return rejectLimit();

    let candidate;
    let position;
    let targetIdentity;
    if (replay) {
      candidate = history.slice();
      candidate[replay.index] = inverse;
      position = replay.position;
      targetIdentity = replay.targetIdentity;
    } else {
      candidate = history.slice(0, historyPosition);
      candidate.push(inverse);
      position = candidate.length;
      targetIdentity = nextIdentity + 1;
    }
    let retained = finalBytes + candidate.reduce((sum, group) => sum + group.bytes, 0);
    let limited = false;
    while (candidate.length > groupLimit || retained > retainedTextLimit) {
      // Retire the farthest past first, or the farthest future when at the base.
      // The replay/new inverse group itself must remain undoable.
      const protectedIndex = replay ? (replay.position < historyPosition ? position : position - 1) : position - 1;
      const remove = position > 0 && protectedIndex !== 0 ? 0 : candidate.length - 1;
      if (remove === protectedIndex) return rejectLimit();
      retained -= candidate[remove].bytes;
      candidate.splice(remove, 1);
      if (remove < position) position--;
      limited = true;
    }

    view.update(transactions);
    history = candidate;
    historyPosition = position;
    identity = targetIdentity;
    if (!replay) nextIdentity = targetIdentity;
    modelVersion++;
    historyLimited = historyLimited || limited;
    // A new source invalidates all old caller ranges/messages, not just positions.
    refreshDiagnostics();
    report();
    return true;
  }

  function travel(backward) {
    requireMutable();
    if (readOnly) return true;
    const index = backward ? historyPosition - 1 : historyPosition;
    const group = history[index];
    if (!group) return true;
    let state = view.state;
    const transactions = [];
    for (let index = 0; index < group.steps.length; index++) {
      const transaction = state.update({
        changes: group.steps[index],
        ...(index === group.steps.length - 1 ? { selection: EditorSelection.fromJSON(group.selection) } : {}),
        scrollIntoView: true
      });
      transactions.push(transaction);
      state = transaction.state;
    }
    acceptTransactions(transactions, {
      index,
      position: backward ? historyPosition - 1 : historyPosition + 1,
      targetIdentity: group.targetIdentity
    });
    return true;
  }

  function completionSource(context) {
    if (disposed || readOnly) return null;
    const prefix = context.matchBefore(representation === "environment" ? /[A-Z0-9_]*/u : /"?[\w./~:\-]*/u);
    if (!prefix || (!prefix.text && !context.explicit)) return null;
    if (representation === "environment" && prefix.from !== context.state.doc.lineAt(context.pos).from) return null;
    const quoted = representation === "canonical" && prefix.text.startsWith('"');
    const keyPrefix = quoted ? prefix.text.slice(1) : prefix.text;
    let to = context.pos;
    if (quoted && context.state.sliceDoc(to, to + 1) === '"') {
      to++;
      const suffix = context.state.sliceDoc(to, context.state.doc.lineAt(to).to).match(/^[ \t]*:[ \t]*/u);
      if (suffix) to += suffix[0].length;
    }
    const matches = completions.filter(item => item.label.startsWith(keyPrefix));
    return matches.length ? { from: prefix.from, to, options: matches, filter: false } : null;
  }

  const representation = options?.representation;
  try {
    // Consume the source before validating other options, so failed mounting
    // cannot leave an acquired answer buffer retained by this adapter.
    initialText = consumeSource(options?.source);
    if (!element || typeof element.appendChild !== "function"
      || !["canonical", "environment"].includes(representation)) {
      throw new TypeError("Invalid editor host or representation.");
    }
    const label = publicText(options.label, 1024, true);
    const describedBy = publicText(options.describedBy ?? "", 2048);
    const locale = publicText(options.locale ?? "en", 64, true);
    if (options.onChange != null && typeof options.onChange !== "function") {
      throw new TypeError("Invalid editor change callback.");
    }
    onChange = options.onChange ?? null;
    phrases = publicPhrases(options.phrases);
    if (!Array.isArray(options.completionItems ?? []) || (options.completionItems?.length ?? 0) > 2048) {
      throw new TypeError("Invalid editor completion metadata.");
    }
    completions = (options.completionItems ?? []).map(item => {
      if (!item || typeof item !== "object") throw new TypeError("Invalid editor completion.");
      const label = publicText(item.label, 1024, true);
      const apply = publicText(item.apply ?? label, 4096);
      const detail = publicText(item.detail ?? "", 1024);
      const type = publicText(item.type ?? "property", 32, true);
      if (!["property", "variable", "constant", "keyword", "text", "enum"].includes(type)
        || (representation === "environment" && !/^[A-Z][A-Z0-9_]{0,127}$/u.test(label))) {
        throw new TypeError("Invalid editor completion kind or key.");
      }
      return { label, apply, detail, type };
    });
    options = null;
    status = element.ownerDocument.createElement("div");
    status.className = "configuration-developer__history-status";
    status.setAttribute("role", "status");
    status.setAttribute("aria-live", "polite");
    status.lang = locale;
    view = new EditorView({
      parent: element,
      state: EditorState.create({
        doc: initialText,
        extensions: [
          EditorState.lineSeparator.of("\n"),
          metadataCompartment.of(metadataExtensions(label, describedBy, locale)),
          readOnlyCompartment.of([EditorState.readOnly.of(false), EditorView.editable.of(true)]),
          lineNumbers(),
          drawSelection(),
          EditorView.lineWrapping,
          representation === "canonical" ? json() : environmentLanguage,
          syntaxHighlighting(themedHighlightStyle),
          EditorView.theme({
            ".cm-gutters": {
              backgroundColor: "var(--isl-color-surface-muted)",
              color: "var(--isl-color-text-secondary)",
              borderColor: "var(--isl-color-border)"
            },
            ".cm-cursor, .cm-dropCursor": { borderLeftColor: "var(--isl-color-text)" }
          }),
          search(),
          lintGutter(),
          autocompletion({ override: [completionSource], defaultKeymap: false }),
          keymap.of([
            { key: "Mod-z", run: () => travel(true) },
            { key: "Mod-Shift-z", run: () => travel(false) },
            { key: "Mod-y", run: () => travel(false) },
            // Neither the editor nor the completion picker owns Tab.
            ...completionKeymap.filter(binding => ![binding.key, binding.mac, binding.win, binding.linux]
              .some(key => key?.includes("Tab"))),
            ...searchKeymap,
            ...defaultKeymap.filter(binding => ![binding.key, binding.mac, binding.win, binding.linux]
              .some(key => key?.includes("Tab")))
          ])
        ]
      }),
      dispatchTransactions: transactions => { acceptTransactions(transactions); }
    });
    initialText = "";
    element.appendChild(status);
    element = null;
    refreshDiagnostics();
  } catch (error) {
    onChange = null;
    options = null;
    initialText = "";
    completions = [];
    phrases = {};
    view?.destroy();
    view = null;
    status?.remove();
    status = null;
    element = null;
    throw error;
  }

  return {
    read(expectedVersion) {
      requireLive();
      if (!Number.isSafeInteger(expectedVersion) || expectedVersion !== modelVersion) {
        throw new Error("Stale editor version.");
      }
      return new TextEncoder().encode(view.state.doc.toString());
    },
    isCurrent(version) {
      return !disposed && Number.isSafeInteger(version) && version === modelVersion;
    },
    setMetadata(label, describedBy, locale, translations) {
      requireMutable();
      label = publicText(label, 1024, true);
      describedBy = publicText(describedBy ?? "", 2048);
      locale = publicText(locale, 64, true);
      const localized = publicPhrases(translations);
      const reopenSearch = searchPanelOpen(view.state);
      const focused = view.dom.ownerDocument.activeElement;
      if (reopenSearch) closeSearchPanel(view);
      phrases = localized;
      view.dispatch({ effects: metadataCompartment.reconfigure(metadataExtensions(label, describedBy, locale)) });
      status.lang = locale;
      status.textContent = historyLimited ? phrases.historyLimited : "";
      if (reopenSearch) {
        openSearchPanel(view);
        if (focused?.isConnected) focused.focus({ preventScroll: true });
      }
    },
    setReadOnly(value) {
      requireMutable();
      if (typeof value !== "boolean") throw new TypeError("Read-only state must be boolean.");
      readOnly = value;
      view.dispatch({
        effects: readOnlyCompartment.reconfigure([EditorState.readOnly.of(value), EditorView.editable.of(!value)])
      });
    },
    setDiagnostics(list) {
      requireMutable();
      if (!Array.isArray(list) || list.length > diagnosticLimit) throw new RangeError("Editor diagnostic limit exceeded.");
      const diagnostics = list.map(item => {
        if (!item || !Number.isInteger(item.from) || !Number.isInteger(item.to)
          || item.from < 0 || item.to < item.from || item.to > view.state.doc.length) {
          throw new RangeError("Invalid editor diagnostic range.");
        }
        const severity = item.severity ?? "error";
        if (!["error", "warning", "info", "hint"].includes(severity)) {
          throw new TypeError("Invalid editor diagnostic severity.");
        }
        return { from: item.from, to: item.to, severity, message: publicText(item.message, 2048, true) };
      });
      refreshDiagnostics(diagnostics);
    },
    replaceSource(bytes) {
      let text = consumeSource(bytes);
      try {
        requireMutable();
        if (readOnly) throw new Error("Editor is read-only.");
        const transaction = view.state.update({ changes: { from: 0, to: view.state.doc.length, insert: text } });
        if (!acceptTransactions([transaction])) throw new RangeError("Editor retained-text limit exceeded.");
        return modelVersion;
      } finally {
        text = "";
      }
    },
    markApplied() {
      requireMutable();
      appliedIdentity = identity;
      report();
    },
    focus() {
      requireLive();
      view.focus();
    },
    dispose() {
      if (disposed) return;
      disposed = true;
      onChange = null;
      history = [];
      historyPosition = 0;
      completions = [];
      phrases = {};
      status?.remove();
      status = null;
      // Remove answer-bearing document, search query and diagnostic state before
      // destroying the view. No caller can retain the underlying EditorView.
      try {
        view.setState(EditorState.create({ doc: "" }));
      } finally {
        view.destroy();
        view = null;
      }
    },
    get version() {
      requireLive();
      return modelVersion;
    },
    get dirty() {
      requireLive();
      return identity !== appliedIdentity;
    }
  };
}
