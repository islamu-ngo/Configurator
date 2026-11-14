const key = 'configurator.appearance.v1';
const modes = new Set(['system', 'light', 'dark', 'lighthighcontrast', 'darkhighcontrast']);
const directions = new Set(['auto', 'ltr', 'rtl']);
const densities = new Set(['spacious', 'comfortable', 'compact', 'dense']);
const namingModes = new Set(['humanized', 'technical']);
const allowedKeys = new Set(['themeMode', 'direction', 'density', 'namingMode']);
const media = window.matchMedia('(prefers-color-scheme: dark)');
let listener;
let activeMode = 'system';
let activeDirection = 'auto';
let activeDensity = 'comfortable';

function parse(raw) {
    if (typeof raw !== 'string' || raw.length > 256) return null;
    try {
        const value = JSON.parse(raw);
        if (!value || Array.isArray(value)) return null;
        const keys = Object.keys(value);
        if (keys.length < 2 || keys.length > 4 || keys.some(name => !allowedKeys.has(name))) return null;
        if (!modes.has(value.themeMode) || !directions.has(value.direction)) return null;
        const density = value.density ?? 'comfortable';
        const namingMode = value.namingMode ?? 'humanized';
        if (!densities.has(density) || !namingModes.has(namingMode)) return null;
        // Reject duplicate JSON properties before persisting or applying canonical values.
        for (const name of keys) {
            if ((raw.match(new RegExp(`"${name}"\\s*:`, 'g')) || []).length !== 1) return null;
        }
        return { themeMode: value.themeMode, direction: value.direction, density, namingMode };
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

export function write(themeMode, direction, density = 'comfortable', namingMode = 'humanized') {
    if (!modes.has(themeMode) || !directions.has(direction) || !densities.has(density) || !namingModes.has(namingMode)) return;
    try { localStorage.setItem(key, JSON.stringify({ themeMode, direction, density, namingMode })); }
    catch { /* Preferences are optional; configuration stays usable without storage. */ }
}

function automaticRtl() {
    return /^(ar|fa|he|ur)(-|$)/i.test(navigator.language || 'en');
}

function colorScheme() {
    return activeMode === 'dark' || activeMode === 'darkhighcontrast' ||
        (activeMode === 'system' && media.matches) ? 'dark' : 'light';
}

export function apply(themeMode, direction, density = 'comfortable') {
    if (!modes.has(themeMode) || !directions.has(direction) || !densities.has(density)) return;
    activeMode = themeMode;
    activeDirection = direction;
    activeDensity = density;
    document.documentElement.dir = direction === 'auto' ? (automaticRtl() ? 'rtl' : 'ltr') : direction;
    document.documentElement.dataset.density = density;
    document.documentElement.classList.toggle('appearance--dark', colorScheme() === 'dark');
    document.documentElement.classList.toggle('appearance--light', colorScheme() === 'light');
}

export function watch(reference) {
    unwatch();
    listener = () => {
        apply(activeMode, activeDirection, activeDensity);
        reference.invokeMethodAsync('SystemPreferencesChanged', media.matches, automaticRtl()).catch(() => {});
    };
    media.addEventListener('change', listener);
    listener();
}

export function unwatch() {
    if (listener) media.removeEventListener('change', listener);
    listener = null;
}
