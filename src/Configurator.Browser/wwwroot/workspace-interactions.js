const listeners = new Map();

export function focusEntry(root) {
    const input = root.querySelector("input");
    const target = input?.getClientRects().length ? input : root.querySelector(".workspace-search__trigger");
    target?.focus({ preventScroll: true });
}

export function watch(owner, root, generation) {
    unwatch(generation);
    const keydown = event => {
        if (!event.isComposing && !event.altKey && (event.ctrlKey || event.metaKey) &&
            event.key.toLowerCase() === "k") {
            event.preventDefault();
            owner.invokeMethodAsync("OpenAsync", generation);
        }
    };
    const outside = event => {
        if (!root.contains(event.target))
            owner.invokeMethodAsync("DismissAsync", generation);
    };
    document.addEventListener("keydown", keydown);
    document.addEventListener("pointerdown", outside);
    listeners.set(generation, { keydown, outside });
}

export function unwatch(generation) {
    const previous = listeners.get(generation);
    if (!previous) return;
    document.removeEventListener("keydown", previous.keydown);
    document.removeEventListener("pointerdown", previous.outside);
    listeners.delete(generation);
}
