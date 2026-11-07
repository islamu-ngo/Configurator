export function paintWorkspace(owner, generation) {
    if (!owner?.isConnected || owner.dataset.startupGeneration !== generation) return;
    return nextFrame();
}

export function nextFrame() {
    // Queue a task from the rendering callback, rather than resuming in its
    // microtask checkpoint. The rendering task can finish before the next batch.
    return new Promise(resolve => {
        requestAnimationFrame(() => {
            nextTask().then(resolve);
        });
    });
}

export function nextTask() {
    // Comparison batches need an event-loop turn, not one display frame per batch.
    return new Promise(resolve => {
        const signal = new MessageChannel();
        signal.port1.onmessage = () => {
            signal.port1.close();
            signal.port2.close();
            resolve();
        };
        signal.port2.postMessage(null);
    });
}

export function focusRadio(element, preventScroll) {
    if (!element?.isConnected) return;
    const input = element.querySelector('input[type="radio"]:checked:not(:disabled)')
        ?? element.querySelector('input[type="radio"]:not(:disabled)');
    if (input?.isConnected) input.focus({ preventScroll });
}

export function revealFocusedControl(owner, generation) {
    if (!owner?.isConnected || owner.dataset.mountGeneration !== generation
        || owner.dataset.pendingFields !== '0') return;
    // Read focus at settlement; never retain or restore the earlier focus target.
    const current = owner.ownerDocument.activeElement;
    if (current && owner.contains(current)) {
        current.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'instant' });
    }
}
