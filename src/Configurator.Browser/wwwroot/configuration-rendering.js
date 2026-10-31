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
