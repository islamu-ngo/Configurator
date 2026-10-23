const listeners = new Map();

export function focusEntry(root) {
    const input = root.querySelector("input");
    const target = input?.getClientRects().length ? input : root.querySelector(".workspace-search__trigger");
    target?.focus({ preventScroll: true });
}

export function returnFocus(root, generation) {
    const previous = listeners.get(generation);
    const bounds = root.getBoundingClientRect();
    if ((bounds.bottom <= 0 || bounds.top >= innerHeight) && previous?.returnTarget?.isConnected)
        previous.returnTarget.focus({ preventScroll: true });
    else
        focusEntry(root);
    if (previous) previous.returnTarget = null;
}

export function watch(owner, root, generation) {
    unwatch(generation);
    const keydown = event => {
        if (!document.querySelector('[role="dialog"]') &&
            !event.isComposing && !event.altKey && (event.ctrlKey || event.metaKey) &&
            event.key.toLowerCase() === "k") {
            event.preventDefault();
            if (!root.contains(document.activeElement))
                listeners.get(generation).returnTarget = document.activeElement;
            owner.invokeMethodAsync("OpenAsync", generation);
        }
    };
    const outside = event => {
        if (!root.contains(event.target))
            owner.invokeMethodAsync("DismissAsync", generation);
    };
    document.addEventListener("keydown", keydown);
    document.addEventListener("pointerdown", outside);
    listeners.set(generation, { keydown, outside, returnTarget: null });
}

export function unwatch(generation) {
    const previous = listeners.get(generation);
    if (!previous) return;
    document.removeEventListener("keydown", previous.keydown);
    document.removeEventListener("pointerdown", previous.outside);
    listeners.delete(generation);
}
