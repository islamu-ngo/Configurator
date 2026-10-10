const key = 'configurator.appearance.v1';
const modes = new Set(['system', 'light', 'dark', 'lighthighcontrast', 'darkhighcontrast']);
const directions = new Set(['auto', 'ltr', 'rtl']);
const media = window.matchMedia('(prefers-color-scheme: dark)');
let listener;
let activeMode = 'system';

function parse(raw) {
    if (typeof raw !== 'string' || raw.length > 256) return null;
    try {
        const value = JSON.parse(raw);
        if (!value || Array.isArray(value) || Object.keys(value).length !== 2 ||
            !modes.has(value.themeMode) || !directions.has(value.direction)) return null;
        // Comparing canonical keys also rejects duplicate-property input before retaining it.
        if ((raw.match(/"themeMode"\s*:/g) || []).length !== 1 ||
            (raw.match(/"direction"\s*:/g) || []).length !== 1) return null;
        return { themeMode: value.themeMode, direction: value.direction };
    } catch { return null; }
}

export function read() {
    try {
        const raw = localStorage.getItem(key);
        const value = parse(raw);
        if (!value) { localStorage.removeItem(key); return null; }
        const canonical = JSON.stringify(value);
        localStorage.setItem(key, canonical);
        return canonical;
    } catch { return null; }
}

export function write(themeMode, direction) {
    if (!modes.has(themeMode) || !directions.has(direction)) return;
    try { localStorage.setItem(key, JSON.stringify({ themeMode, direction })); }
    catch { /* Preferences are optional; configuration stays usable without storage. */ }
}

function automaticRtl() {
    return /^(ar|fa|he|ur)(-|$)/i.test(navigator.language || 'en');
}

function colorScheme() {
    return activeMode === 'dark' || activeMode === 'darkhighcontrast' ||
        (activeMode === 'system' && media.matches) ? 'dark' : 'light';
}

export function apply(themeMode, direction) {
    if (!modes.has(themeMode) || !directions.has(direction)) return;
    activeMode = themeMode;
    document.documentElement.dir = direction === 'auto' ? (automaticRtl() ? 'rtl' : 'ltr') : direction;
    document.documentElement.classList.toggle('appearance--dark', colorScheme() === 'dark');
    document.documentElement.classList.toggle('appearance--light', colorScheme() === 'light');
}

export function watch(reference) {
    unwatch();
    listener = () => {
        apply(activeMode, document.documentElement.dir);
        reference.invokeMethodAsync('SystemPreferencesChanged', media.matches, automaticRtl()).catch(() => {});
    };
    media.addEventListener('change', listener);
    listener();
}

export function unwatch() {
    if (listener) media.removeEventListener('change', listener);
    listener = null;
}
