const key = 'configurator.appearance.v1';
const modes = new Set(['system', 'light', 'dark', 'lighthighcontrast', 'darkhighcontrast']);
const presets = new Set([
    'enterprise-blue', 'emerald-green', 'abyssal-dark', 'pure-white',
    'classic-white', 'classic-dark', 'light-hc', 'dark-hc', 'custom'
]);
const directions = new Set(['auto', 'ltr', 'rtl']);
const densities = new Set(['spacious', 'comfortable', 'compact', 'dense']);
const namingModes = new Set(['humanized', 'technical']);
const allowedKeys = new Set([
    'themeMode', 'themeKey', 'customNaturalHex', 'customBrandHex',
    'direction', 'density', 'namingMode'
]);
const hexPattern = /^#?(?:[0-9a-f]{3}|[0-9a-f]{6})$/i;
const media = window.matchMedia('(prefers-color-scheme: dark)');
const contrastMedia = window.matchMedia('(prefers-contrast: more)');
let listener;
let activeMode = 'system';
let activeDirection = 'auto';
let activeDensity = 'comfortable';
let activeThemeKey = 'enterprise-blue';

function normalizeHex(value) {
    if (typeof value !== 'string') return null;
    const trimmed = value.trim();
    if (!hexPattern.test(trimmed)) return null;
    const raw = trimmed.startsWith('#') ? trimmed.slice(1) : trimmed;
    const expanded = raw.length === 3
        ? `${raw[0]}${raw[0]}${raw[1]}${raw[1]}${raw[2]}${raw[2]}`
        : raw;
    return `#${expanded.toUpperCase()}`;
}

function parse(raw) {
    if (typeof raw !== 'string' || raw.length > 512) return null;
    try {
        const value = JSON.parse(raw);
        if (!value || Array.isArray(value) || typeof value !== 'object') return null;
        const keys = Object.keys(value);
        if (keys.length < 2 || keys.length > 7 || keys.some(name => !allowedKeys.has(name))) return null;
        for (const name of keys) {
            if (typeof value[name] !== 'string') return null;
            if ((raw.match(new RegExp(`"${name}"\\s*:`, 'g')) || []).length !== 1) return null;
        }
        if (!modes.has(value.themeMode) || !directions.has(value.direction)) return null;
        const themeKey = value.themeKey ?? 'enterprise-blue';
        const density = value.density ?? 'comfortable';
        const namingMode = value.namingMode ?? 'humanized';
        if (!presets.has(themeKey) || !densities.has(density) || !namingModes.has(namingMode)) return null;
        const customNaturalHex = value.customNaturalHex !== undefined ? normalizeHex(value.customNaturalHex) : '#475569';
        const customBrandHex = value.customBrandHex !== undefined ? normalizeHex(value.customBrandHex) : '#3B82F6';
        if (!customNaturalHex || !customBrandHex) return null;
        if (themeKey === 'custom' && (value.customNaturalHex === undefined || value.customBrandHex === undefined)) return null;
        return {
            themeMode: value.themeMode,
            themeKey,
            customNaturalHex,
            customBrandHex,
            direction: value.direction,
            density,
            namingMode
        };
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

export function write(
    themeMode,
    direction,
    density = 'comfortable',
    namingMode = 'humanized',
    themeKey = 'enterprise-blue',
    customNaturalHex = '#475569',
    customBrandHex = '#3B82F6'
) {
    const normalizedNatural = normalizeHex(customNaturalHex);
    const normalizedBrand = normalizeHex(customBrandHex);
    if (!modes.has(themeMode) || !directions.has(direction) || !densities.has(density)
        || !namingModes.has(namingMode) || !presets.has(themeKey)
        || !normalizedNatural || !normalizedBrand) {
        return;
    }
    try {
        localStorage.setItem(key, JSON.stringify({
            themeMode,
            themeKey,
            customNaturalHex: normalizedNatural,
            customBrandHex: normalizedBrand,
            direction,
            density,
            namingMode
        }));
    } catch { /* Preferences are optional; configuration stays usable without storage. */ }
}

function automaticRtl() {
    return /^(ar|fa|he|ur)(-|$)/i.test(navigator.language || 'en');
}

function resolveScheme(themeMode, themeKey) {
    const isDark = themeMode === 'dark' || themeMode === 'darkhighcontrast'
        || (themeMode === 'system' && (themeKey === 'dark-hc' || (themeKey !== 'light-hc' && media.matches)));
    const isLightHc = themeMode === 'lighthighcontrast'
        || (themeMode === 'system' && (themeKey === 'light-hc' || (contrastMedia.matches && !isDark)));
    const isDarkHc = themeMode === 'darkhighcontrast'
        || (themeMode === 'system' && (themeKey === 'dark-hc' || (contrastMedia.matches && isDark)));
    return { isDark, isLightHc, isDarkHc };
}

export function apply(
    themeMode,
    direction,
    density = 'comfortable',
    _namingMode = 'humanized',
    themeKey = 'enterprise-blue'
) {
    if (!modes.has(themeMode) || !directions.has(direction) || !densities.has(density) || !presets.has(themeKey)) return;
    activeMode = themeMode;
    activeDirection = direction;
    activeDensity = density;
    activeThemeKey = themeKey;
    const root = document.documentElement;
    root.dir = direction === 'auto' ? (automaticRtl() ? 'rtl' : 'ltr') : direction;
    root.dataset.density = density;
    root.dataset.themePreset = themeKey;
    const scheme = resolveScheme(themeMode, themeKey);
    root.classList.toggle('appearance--dark', scheme.isDark);
    root.classList.toggle('appearance--light', !scheme.isDark);
    root.classList.toggle('appearance--light-hc', scheme.isLightHc);
    root.classList.toggle('appearance--dark-hc', scheme.isDarkHc);
}

export function watch(reference) {
    unwatch();
    listener = () => {
        apply(activeMode, activeDirection, activeDensity, 'humanized', activeThemeKey);
        reference.invokeMethodAsync('SystemPreferencesWithContrastChanged', media.matches, automaticRtl(), contrastMedia.matches).catch(() => {});
    };
    media.addEventListener('change', listener);
    contrastMedia.addEventListener('change', listener);
    listener();
}

export function unwatch() {
    if (listener) {
        media.removeEventListener('change', listener);
        contrastMedia.removeEventListener('change', listener);
    }
    listener = null;
}
