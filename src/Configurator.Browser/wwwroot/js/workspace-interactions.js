const listeners = new Map();

export function focusEntry(root) {
    const input = root.querySelector("input");
    const target = input?.getClientRects().length ? input : root.querySelector(".workspace-search__trigger");
    target?.focus({ preventScroll: true });
}

export function returnFocus(root, generation) {
    const previous = listeners.get(generation);
    const target = previous?.returnTarget?.deref();
    if (target?.isConnected)
        target.focus({ preventScroll: true });
    else
        focusEntry(root);
    if (previous) previous.returnTarget = null;
}

export function watch(owner, root, generation) {
    unwatch(generation);
    const isTextEntry = element => {
        if (!element) return false;
        const tag = element.tagName;
        return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || element.isContentEditable;
    };
    const keydown = event => {
        if (document.querySelector('[role="dialog"]') || event.isComposing) return;
        if (!event.altKey && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
            event.preventDefault();
            if (!root.contains(document.activeElement))
                listeners.get(generation).returnTarget = new WeakRef(document.activeElement);
            owner.invokeMethodAsync("OpenAsync", generation);
            return;
        }
        if (!event.ctrlKey && !event.metaKey && !event.altKey && event.key === "/" && !isTextEntry(document.activeElement)) {
            event.preventDefault();
            if (!root.contains(document.activeElement))
                listeners.get(generation).returnTarget = new WeakRef(document.activeElement);
            owner.invokeMethodAsync("OpenAsync", generation);
        }
    };
    const rootKeydown = event => {
        if (event.key === "ArrowDown" || event.key === "ArrowUp")
            event.preventDefault();
    };
    const outside = event => {
        if (!root.contains(event.target)) {
            listeners.get(generation).returnTarget = null;
            owner.invokeMethodAsync("DismissAsync", generation);
        }
    };
    document.addEventListener("keydown", keydown);
    root.addEventListener("keydown", rootKeydown);
    document.addEventListener("pointerdown", outside);
    listeners.set(generation, { root, keydown, rootKeydown, outside, returnTarget: null });
}

export function unwatch(generation) {
    const previous = listeners.get(generation);
    if (!previous) return;
    document.removeEventListener("keydown", previous.keydown);
    previous.root?.removeEventListener("keydown", previous.rootKeydown);
    document.removeEventListener("pointerdown", previous.outside);
    listeners.delete(generation);
}
