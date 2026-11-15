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

const observedToolbars = new WeakMap();

export function syncCompactToolbarClearance(owner) {
    if (!owner?.isConnected) return;
    const workspace = owner.closest('.configuration-compact-workspace') ?? owner;
    const toolbar = workspace.querySelector('.configuration-compact-workspace__toolbar');
    if (!toolbar) {
        workspace.style.removeProperty('--isl-compact-toolbar-block-size');
        return;
    }

    const view = workspace.ownerDocument.defaultView;
    const sticky = view?.getComputedStyle(toolbar).position === 'sticky';
    const height = sticky ? Math.ceil(toolbar.getBoundingClientRect().height) : 0;
    workspace.style.setProperty('--isl-compact-toolbar-block-size', `${height}px`);

    if (!observedToolbars.has(toolbar) && typeof ResizeObserver === 'function') {
        const observer = new ResizeObserver(() => {
            if (!workspace.isConnected || !toolbar.isConnected) {
                observer.disconnect();
                observedToolbars.delete(toolbar);
                return;
            }

            const currentView = workspace.ownerDocument.defaultView;
            const currentSticky = currentView?.getComputedStyle(toolbar).position === 'sticky';
            const currentHeight = currentSticky ? Math.ceil(toolbar.getBoundingClientRect().height) : 0;
            workspace.style.setProperty('--isl-compact-toolbar-block-size', `${currentHeight}px`);
        });
        observer.observe(toolbar);
        observedToolbars.set(toolbar, observer);
    }
}

export function revealHeading(owner, headingId) {
    if (!owner?.isConnected || !headingId) return;
    owner._cancelRevealHeading?.();

    const doc = owner.ownerDocument;
    const win = doc.defaultView;
    const heading = doc.getElementById(headingId);
    if (!heading || !owner.contains(heading)) return;

    syncCompactToolbarClearance(owner);
    heading.scrollIntoView({ block: 'start', inline: 'nearest', behavior: 'instant' });
    heading.focus({ preventScroll: true });

    if (!win?.requestAnimationFrame) return;

    const now = () => (win.performance?.now ? win.performance.now() : Date.now());
    const deadline = now() + 600;
    let rafId = 0;
    let timeoutId = 0;
    let resizeObserver = null;
    let quietFrames = 0;
    let sawResize = false;
    let disposed = false;

    const cleanup = () => {
        if (disposed) return;
        disposed = true;
        if (rafId) {
            win.cancelAnimationFrame(rafId);
            rafId = 0;
        }
        if (timeoutId) {
            win.clearTimeout(timeoutId);
            timeoutId = 0;
        }
        resizeObserver?.disconnect();
        resizeObserver = null;
        doc.removeEventListener('wheel', cleanup, true);
        doc.removeEventListener('touchstart', cleanup, true);
        doc.removeEventListener('pointerdown', cleanup, true);
        doc.removeEventListener('keydown', cleanup, true);
        if (owner._cancelRevealHeading === cleanup) {
            delete owner._cancelRevealHeading;
        }
    };

    const isSettled = () => {
        syncCompactToolbarClearance(owner);
        const marginTop = Number.parseFloat(win.getComputedStyle(heading).scrollMarginTop) || 0;
        const rect = heading.getBoundingClientRect();
        const atBottom = Math.ceil(win.scrollY + win.innerHeight) >= doc.documentElement.scrollHeight - 1;
        return Math.abs(rect.top - marginTop) <= 2 || (atBottom && rect.top >= marginTop - 1);
    };

    const scheduleFrame = () => {
        if (disposed || rafId || now() >= deadline) return;
        rafId = win.requestAnimationFrame(align);
    };

    const align = () => {
        rafId = 0;
        if (disposed || !owner.isConnected || !heading.isConnected || doc.activeElement !== heading) {
            cleanup();
            return;
        }

        const resizedSinceLastFrame = sawResize;
        sawResize = false;
        const settled = isSettled();
        if (!settled) {
            heading.scrollIntoView({ block: 'start', inline: 'nearest', behavior: 'instant' });
            quietFrames = 0;
        } else if (resizedSinceLastFrame) {
            quietFrames = 0;
        } else {
            quietFrames += 1;
        }

        if (now() >= deadline) {
            cleanup();
            return;
        }

        if (quietFrames < 4) {
            scheduleFrame();
        }
    };

    owner._cancelRevealHeading = cleanup;
    doc.addEventListener('wheel', cleanup, { capture: true, passive: true });
    doc.addEventListener('touchstart', cleanup, { capture: true, passive: true });
    doc.addEventListener('pointerdown', cleanup, { capture: true, passive: true });
    doc.addEventListener('keydown', cleanup, { capture: true, passive: true });

    if (typeof win.ResizeObserver === 'function') {
        resizeObserver = new win.ResizeObserver(() => {
            if (disposed) return;
            if (!owner.isConnected || !heading.isConnected || doc.activeElement !== heading) {
                cleanup();
                return;
            }
            sawResize = true;
            quietFrames = 0;
            scheduleFrame();
        });
        resizeObserver.observe(owner);
    }

    timeoutId = win.setTimeout(cleanup, 600);
    scheduleFrame();
}
