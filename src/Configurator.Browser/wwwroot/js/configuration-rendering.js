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

const headerObservers = new WeakMap();

function measureHeaderOffset(doc) {
    if (!doc?.documentElement) return 0;
    const header = doc.querySelector('.nav-menu__header.navbar');
    if (!header) {
        doc.documentElement.style.removeProperty('--isl-shell-header-actual-height');
        return 0;
    }

    const height = Math.ceil(header.getBoundingClientRect().height);
    if (height > 0) {
        doc.documentElement.style.setProperty('--isl-shell-header-actual-height', `${height}px`);
    }
    return height;
}

export function observeHeaderHeight(owner) {
    unobserveHeaderHeight(owner);
    if (!owner?.isConnected) return;

    const doc = owner.ownerDocument;
    const win = doc.defaultView;
    const header = doc.querySelector('.nav-menu__header.navbar');
    measureHeaderOffset(doc);

    const Observer = win?.ResizeObserver ?? (typeof ResizeObserver === 'function' ? ResizeObserver : null);
    if (!header || !Observer) return;

    const observer = new Observer(() => {
        if (!owner.isConnected || !header.isConnected) {
            unobserveHeaderHeight(owner);
            return;
        }
        measureHeaderOffset(doc);
    });
    observer.observe(header);
    headerObservers.set(owner, { header, observer });
}

export function unobserveHeaderHeight(owner) {
    if (!owner) return;
    const entry = headerObservers.get(owner);
    if (!entry) return;
    entry.observer.disconnect();
    headerObservers.delete(owner);
}

export function syncActiveNavScroll(navElement) {
    if (!navElement?.isConnected) return;
    if (navElement.scrollHeight <= navElement.clientHeight) return;

    const active = navElement.querySelector('[aria-current="step"], [aria-current="location"]');
    if (!active || !navElement.contains(active)) return;

    const navRect = navElement.getBoundingClientRect();
    const itemRect = active.getBoundingClientRect();
    const cushion = 8;

    if (itemRect.top < navRect.top + cushion) {
        navElement.scrollTop -= (navRect.top + cushion) - itemRect.top;
    } else if (itemRect.bottom > navRect.bottom - cushion) {
        navElement.scrollTop += itemRect.bottom - (navRect.bottom - cushion);
    }
}

export function revealHeading(owner, headingId) {
    if (!owner?.isConnected || !headingId) return;
    owner._cancelRevealHeading?.();

    const doc = owner.ownerDocument;
    const win = doc.defaultView;
    const heading = doc.getElementById(headingId);
    if (!heading || !owner.contains(heading)) return;

    const now = () => (win?.performance?.now ? win.performance.now() : Date.now());
    owner._scrollspyCooldownUntil = now() + 650;

    measureHeaderOffset(doc);
    heading.scrollIntoView({ block: 'start', inline: 'nearest', behavior: 'instant' });
    heading.focus({ preventScroll: true });

    if (!win?.requestAnimationFrame) return;

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
        doc.removeEventListener('wheel', onUserScroll, true);
        doc.removeEventListener('touchstart', onUserScroll, true);
        doc.removeEventListener('pointerdown', onUserScroll, true);
        doc.removeEventListener('keydown', onUserScroll, true);
        if (owner._cancelRevealHeading === cleanup) {
            delete owner._cancelRevealHeading;
        }
    };

    const onUserScroll = () => {
        owner._scrollspyCooldownUntil = 0;
        cleanup();
    };

    const isSettled = () => {
        measureHeaderOffset(doc);
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
            owner._scrollspyCooldownUntil = 0;
            cleanup();
            return;
        }

        if (quietFrames < 4) {
            scheduleFrame();
        } else {
            owner._scrollspyCooldownUntil = 0;
        }
    };

    owner._cancelRevealHeading = cleanup;
    doc.addEventListener('wheel', onUserScroll, { capture: true, passive: true });
    doc.addEventListener('touchstart', onUserScroll, { capture: true, passive: true });
    doc.addEventListener('pointerdown', onUserScroll, { capture: true, passive: true });
    doc.addEventListener('keydown', onUserScroll, { capture: true, passive: true });

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

    timeoutId = win.setTimeout(() => {
        owner._scrollspyCooldownUntil = 0;
        cleanup();
    }, 600);
    scheduleFrame();
}

const scrollspyObservers = new WeakMap();

export function unobserveScrollspy(owner) {
    if (!owner) return;
    const entry = scrollspyObservers.get(owner);
    if (!entry) return;
    entry.observer?.disconnect();
    if (entry.onScroll && entry.win) {
        entry.win.removeEventListener('scroll', entry.onScroll);
    }
    if (entry.scrollRaf && entry.win) {
        entry.win.cancelAnimationFrame(entry.scrollRaf);
    }
    scrollspyObservers.delete(owner);
}

export function observeScrollspy(owner, dotNetRef, generation) {
    unobserveScrollspy(owner);
    if (!owner?.isConnected || !dotNetRef) return;

    const doc = owner.ownerDocument;
    const win = doc.defaultView;
    const targets = Array.from(owner.querySelectorAll('[data-compact-section], [data-section-panel]'));
    if (targets.length === 0) return;

    const now = () => (win?.performance?.now ? win.performance.now() : Date.now());
    const state = { observer: null, win, onScroll: null, scrollRaf: 0, generation, visible: new Set(), lastId: null };
    const sectionIdOf = element => element.getAttribute('data-compact-section')
        ?? element.getAttribute('data-section-panel');

    const emit = element => {
        const id = element ? sectionIdOf(element) : null;
        if (!id || id === state.lastId) return;
        state.lastId = id;
        dotNetRef.invokeMethodAsync('OnScrollspySectionChangedAsync', id).catch(() => {});
    };

    const pickActive = () => {
        if (!owner.isConnected || scrollspyObservers.get(owner)?.generation !== state.generation) {
            unobserveScrollspy(owner);
            return;
        }
        if (owner._scrollspyCooldownUntil && now() < owner._scrollspyCooldownUntil) return;

        const visibleTargets = targets.filter(el => el.isConnected && !el.hidden);
        if (visibleTargets.length === 0) return;

        const docEl = doc.documentElement;
        const viewportHeight = win?.innerHeight ?? docEl.clientHeight;
        const scrollY = win?.scrollY ?? docEl.scrollTop ?? 0;
        const atBottom = docEl.scrollHeight > viewportHeight + 4
            && Math.ceil(scrollY + viewportHeight) >= docEl.scrollHeight - 4;
        if (atBottom) {
            emit(visibleTargets[visibleTargets.length - 1]);
            return;
        }

        const headerHeight = measureHeaderOffset(doc) || 64;
        const headerClearance = headerHeight + 24;
        let active = null;
        for (const el of visibleTargets) {
            const rect = el.getBoundingClientRect();
            if (rect.top <= headerClearance && rect.bottom > headerClearance) {
                active = el;
            }
        }

        if (!active) {
            const candidates = visibleTargets.filter(el => state.visible.has(el));
            if (candidates.length > 0) {
                active = candidates.reduce((best, item) =>
                    item.getBoundingClientRect().top < best.getBoundingClientRect().top ? item : best);
            } else {
                for (const el of visibleTargets) {
                    if (el.getBoundingClientRect().top <= headerClearance) {
                        active = el;
                    }
                }
                active ??= visibleTargets[0];
            }
        }

        emit(active);
    };

    const Observer = win?.IntersectionObserver ?? (typeof IntersectionObserver === 'function' ? IntersectionObserver : null);
    if (Observer) {
        const initialHeader = measureHeaderOffset(doc) || 64;
        const viewportHeight = win?.innerHeight ?? doc.documentElement.clientHeight ?? 900;
        const topMargin = Math.min(Math.floor(viewportHeight * 0.4), Math.ceil(initialHeader + 16));
        const observer = new Observer(entries => {
            for (const entry of entries) {
                if (entry.isIntersecting) state.visible.add(entry.target);
                else state.visible.delete(entry.target);
            }
            pickActive();
        }, { root: null, rootMargin: `-${topMargin}px 0px -50% 0px`, threshold: 0 });

        state.observer = observer;
        for (const target of targets) observer.observe(target);
    }

    if (win) {
        state.onScroll = () => {
            if (state.scrollRaf) return;
            state.scrollRaf = win.requestAnimationFrame(() => {
                state.scrollRaf = 0;
                pickActive();
            });
        };
        win.addEventListener('scroll', state.onScroll, { passive: true });
    }

    scrollspyObservers.set(owner, state);
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
