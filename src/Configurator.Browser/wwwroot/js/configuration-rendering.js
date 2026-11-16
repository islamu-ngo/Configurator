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

const scrollspyObservers = new WeakMap();

export function unobserveScrollspy(owner) {
    if (!owner) return;
    const entry = scrollspyObservers.get(owner);
    if (!entry) return;
    entry.observer.disconnect();
    scrollspyObservers.delete(owner);
}

export function observeScrollspy(owner, dotNetRef, generation) {
    unobserveScrollspy(owner);
    if (!owner?.isConnected || !dotNetRef || typeof IntersectionObserver !== 'function') return;

    const doc = owner.ownerDocument;
    const win = doc.defaultView;
    const targets = Array.from(owner.querySelectorAll('[data-compact-section], [data-section-panel]'));
    if (targets.length === 0) return;

    const state = { observer: null, generation, visible: new Set(), lastId: null };
    const sectionIdOf = element => element.getAttribute('data-compact-section')
        ?? element.getAttribute('data-section-panel');

    const pickActive = () => {
        if (!owner.isConnected || scrollspyObservers.get(owner)?.generation !== state.generation) {
            unobserveScrollspy(owner);
            return;
        }
        const candidates = targets.filter(el => el.isConnected && !el.hidden && state.visible.has(el));
        if (candidates.length === 0) return;
        const active = candidates.reduce((best, item) =>
            item.getBoundingClientRect().top < best.getBoundingClientRect().top ? item : best);
        const id = sectionIdOf(active);
        if (!id || id === state.lastId) return;
        state.lastId = id;
        dotNetRef.invokeMethodAsync('OnScrollspySectionChangedAsync', id).catch(() => {});
    };

    const observer = new (win?.IntersectionObserver ?? IntersectionObserver)(entries => {
        for (const entry of entries) {
            if (entry.isIntersecting) state.visible.add(entry.target);
            else state.visible.delete(entry.target);
        }
        pickActive();
    }, { root: null, rootMargin: '-18% 0px -65% 0px', threshold: 0 });

    state.observer = observer;
    scrollspyObservers.set(owner, state);
    for (const target of targets) observer.observe(target);
}

export function isNarrowViewport() {
    return typeof window !== 'undefined' && typeof window.matchMedia === 'function'
        ? window.matchMedia('(max-width: 767.98px)').matches
        : false;
}

const inspectorViewports = new WeakMap();

export function watchInspectorViewport(owner, dotNetRef) {
    unwatchInspectorViewport(owner);
    if (!owner?.isConnected || !dotNetRef || typeof window.matchMedia !== 'function') return false;
    const mq = window.matchMedia('(max-width: 767.98px)');
    const handler = event => {
        if (!owner.isConnected) {
            unwatchInspectorViewport(owner);
            return;
        }
        dotNetRef.invokeMethodAsync('OnInspectorViewportModeChangedAsync', !!event.matches).catch(() => {});
    };
    mq.addEventListener('change', handler);
    inspectorViewports.set(owner, { mq, handler });
    return !!mq.matches;
}

export function unwatchInspectorViewport(owner) {
    if (!owner) return;
    const entry = inspectorViewports.get(owner);
    if (!entry) return;
    entry.mq.removeEventListener('change', entry.handler);
    inspectorViewports.delete(owner);
}

export function focusSheetStart(sheet) {
    if (!sheet?.isConnected) return;
    if (!sheet.contains(sheet.ownerDocument.activeElement)) {
        const firstFocusable = sheet.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        (firstFocusable ?? sheet).focus({ preventScroll: true });
    }
    if (!sheet._sheetKeyBound) {
        sheet._sheetKeyBound = true;
        sheet.addEventListener('keydown', event => {
            if (event.key !== 'Tab' || !sheet.isConnected) return;
            const focusables = Array.from(sheet.querySelectorAll('button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'));
            if (focusables.length === 0) {
                event.preventDefault();
                sheet.focus();
                return;
            }
            const first = focusables[0];
            const last = focusables[focusables.length - 1];
            const active = sheet.ownerDocument.activeElement;
            if (event.shiftKey && (active === first || active === sheet)) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && active === last) {
                event.preventDefault();
                first.focus();
            }
        });
    }
}
