let _savedFocusElement = null;

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

export function saveActiveElement() {
    _savedFocusElement = document.activeElement;
}

export function restoreFocus(fallbackSelector) {
    requestAnimationFrame(() => {
        const current = document.activeElement;
        if (current && current !== document.body && !current.closest('[data-dock-overlay-host]')) {
            _savedFocusElement = null;
            return;
        }

        if (_savedFocusElement && _savedFocusElement !== document.body && document.contains(_savedFocusElement)) {
            _savedFocusElement.focus();
            _savedFocusElement = null;
            return;
        }

        _savedFocusElement = null;

        if (fallbackSelector) {
            const fallback = document.querySelector(fallbackSelector);
            if (fallback) {
                fallback.focus();
                return;
            }
        }

        const main = document.getElementById('main-content');
        if (main) {
            main.focus();
            return;
        }

        document.body.focus();
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
