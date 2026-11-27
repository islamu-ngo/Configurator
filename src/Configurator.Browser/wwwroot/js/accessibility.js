const _savedFocusByScope = new Map();
const DOCK_OVERLAY_SELECTOR = '[data-testid="dock-overlay-host"]';
const DOCK_OVERLAY_OR_END_SELECTOR = '[data-testid="dock-overlay-host"], [data-dock-side="end"]';

export function setFocus(selector, preventScroll) {
    requestAnimationFrame(() => {
        const el = document.querySelector(selector);
        if (el) {
            if (el.tabIndex < 0 && !el.hasAttribute('tabindex')) {
                el.setAttribute('tabindex', '-1');
            }
            el.focus({ preventScroll: !!preventScroll });
        }
    });
}

export function setFocusById(elementId, preventScroll) {
    requestAnimationFrame(() => {
        const el = document.getElementById(elementId);
        if (el) {
            if (el.tabIndex < 0 && !el.hasAttribute('tabindex')) {
                el.setAttribute('tabindex', '-1');
            }
            el.focus({ preventScroll: !!preventScroll });
        }
    });
}

export function announce(message, politeness) {
    const regionId = politeness === 'assertive'
        ? 'aria-live-assertive'
        : 'aria-live-polite';

    const region = document.getElementById(regionId);
    if (!region) {
        return;
    }

    region.textContent = '';
    requestAnimationFrame(() => {
        region.textContent = message;
    });
}

export function saveActiveElement(scope = 'default') {
    const key = typeof scope === 'string' && scope.length > 0 ? scope : 'default';
    const active = document.activeElement;
    if (!active || active === document.body) {
        return;
    }

    // Never save an element inside an overlay, or overwrite a saved trigger from inside an end dock.
    if (active.closest?.(DOCK_OVERLAY_SELECTOR)) {
        return;
    }
    if (_savedFocusByScope.has(key) && active.closest?.(DOCK_OVERLAY_OR_END_SELECTOR)) {
        return;
    }

    _savedFocusByScope.set(key, typeof WeakRef === 'function' ? new WeakRef(active) : active);
}

export function restoreFocus(fallbackSelector, preventScroll = false, scope = 'default') {
    const key = typeof scope === 'string' && scope.length > 0 ? scope : 'default';
    const savedRef = _savedFocusByScope.get(key) ?? null;
    _savedFocusByScope.delete(key);
    const focusOptions = { preventScroll: !!preventScroll };

    requestAnimationFrame(() => {
        const current = document.activeElement;
        if (current && current !== document.body && !current.closest?.(DOCK_OVERLAY_OR_END_SELECTOR)) {
            return;
        }

        const saved = savedRef && typeof savedRef.deref === 'function' ? savedRef.deref() : savedRef;
        if (saved && saved !== document.body && saved.isConnected && !saved.closest?.(DOCK_OVERLAY_OR_END_SELECTOR)) {
            saved.focus(focusOptions);
            return;
        }

        if (fallbackSelector) {
            const fallback = document.querySelector(fallbackSelector);
            if (fallback) {
                fallback.focus(focusOptions);
                return;
            }
        }

        const main = document.getElementById('main-content');
        if (main) {
            main.focus(focusOptions);
            return;
        }

        document.body.focus(focusOptions);
    });
}

export function focusOnNavigate() {
    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            const h1 = document.querySelector('h1');
            if (h1) {
                if (h1.tabIndex < 0 && !h1.hasAttribute('tabindex')) {
                    h1.setAttribute('tabindex', '-1');
                }
                h1.focus({ preventScroll: true });
                return;
            }

            const main = document.getElementById('main-content');
            if (main) {
                main.focus({ preventScroll: true });
            }
        });
    });
}

export function getPreferredMotion() {
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        return 'reduce';
    }
    return 'no-preference';
}
