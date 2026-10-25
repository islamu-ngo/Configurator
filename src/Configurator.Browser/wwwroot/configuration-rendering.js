export function nextFrame() {
    // Queue a task from the rendering callback, rather than resuming in its
    // microtask checkpoint. The rendering task can finish before the next batch.
    return new Promise(resolve => {
        requestAnimationFrame(() => {
            const signal = new MessageChannel();
            signal.port1.onmessage = () => {
                signal.port1.close();
                signal.port2.close();
                resolve();
            };
            signal.port2.postMessage(null);
        });
    });
}
