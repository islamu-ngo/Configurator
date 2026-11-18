let pendingFrame = 0;

export function setPointerCapture(element, pointerId) {
    if (!element || typeof element.setPointerCapture !== 'function') {
        return;
    }

    element.setPointerCapture(pointerId);
}

export function releasePointerCapture(element, pointerId) {
    if (!element || typeof element.releasePointerCapture !== 'function') {
        return;
    }

    if (typeof element.hasPointerCapture === 'function' && !element.hasPointerCapture(pointerId)) {
        return;
    }

    element.releasePointerCapture(pointerId);
}

export function schedulePanelWidth(panelElement, widthPx) {
    if (!panelElement || !panelElement.style) {
        return;
    }

    if (pendingFrame) {
        cancelAnimationFrame(pendingFrame);
    }

    pendingFrame = requestAnimationFrame(() => {
        pendingFrame = 0;
        panelElement.style.setProperty('--dock-panel-width', `${widthPx}px`);
    });
}
