import { useSyncExternalStore } from 'react';

// Languages and texts are data, managed in the back office (Platform service): languages can be added, renamed,
// enabled, disabled and deleted, and every text edited, without a release. Before the first render each portal
// loads the enabled languages and the catalogue for the reader's language; the last good copy is kept in the
// browser so the portal still opens if the Platform service is briefly unavailable. A key with no text shows as is.

/** A text in any number of languages, keyed by language code: { en: "Fee", ar: "الرسوم" }. */
export type LocalizedText = Record<string, string>;

export interface LanguageInfo {
  code: string;
  /** The language's name in other languages (each language is listed in its own name). */
  name: LocalizedText;
  direction: 'ltr' | 'rtl';
  isEnabled: boolean;
  isDefault: boolean;
  sortOrder: number;
}

interface Catalog { language: string; defaultLanguage: string; direction: 'ltr' | 'rtl'; strings: Record<string, string> }

/** Message keys are plain strings: texts live in the Platform catalogue. */
export type TKey = string;
export type Lang = string;

const STORAGE_LANG = 'dsp.lang';
const STORAGE_LANGUAGES = 'dsp.languages';
const storageCatalog = (code: string) => `dsp.catalog.${code}`;
const listeners = new Set<() => void>();

let languages: LanguageInfo[] = read<LanguageInfo[]>(STORAGE_LANGUAGES) ?? [];
let current = '';
let defaultLanguage = 'en';
let strings: Record<string, string> = {};
let version = 0;

function read<T>(key: string): T | undefined {
  try { const raw = localStorage.getItem(key); return raw ? JSON.parse(raw) as T : undefined; } catch { return undefined; }
}
function write(key: string, value: unknown) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* storage unavailable */ }
}

async function fetchJson<T>(path: string, lang?: string): Promise<T> {
  const res = await fetch(path, { headers: lang ? { 'Accept-Language': lang } : {} });
  if (!res.ok) throw new Error(`${path}: ${res.status}`);
  return res.json() as Promise<T>;
}

function pick(preferred?: string | null): string {
  const enabled = languages.filter(l => l.isEnabled);
  const find = (code?: string | null) => code && enabled.find(l => l.code.toLowerCase() === code.toLowerCase())?.code;
  const neutral = (code?: string | null) => code && enabled.find(l => l.code.toLowerCase() === code.split('-')[0].toLowerCase())?.code;
  return find(preferred) || neutral(preferred)
    || find(navigator.language) || neutral(navigator.language)
    || enabled.find(l => l.isDefault)?.code || enabled[0]?.code || 'en';
}

function apply(direction: 'ltr' | 'rtl') {
  document.documentElement.lang = current;
  document.documentElement.dir = direction;
}

function adopt(catalog: Catalog) {
  current = catalog.language;
  defaultLanguage = catalog.defaultLanguage;
  strings = catalog.strings;
  apply(catalog.direction);
  version++;
  listeners.forEach(l => l());
}

async function loadCatalog(code: string): Promise<Catalog> {
  try {
    const catalog = await fetchJson<Catalog>(`/api/v1/platform/catalog/${encodeURIComponent(code)}`);
    write(storageCatalog(catalog.language), catalog);
    return catalog;
  } catch (e) {
    const cached = read<Catalog>(storageCatalog(code));
    if (cached) return cached;
    throw e;
  }
}

/**
 * Loads the enabled languages and the catalogue for the reader's language. Each portal awaits it before rendering
 * (mountApp does). Uses the browser's last good copy when the Platform service can't be reached.
 */
export async function initI18n(): Promise<void> {
  try {
    languages = await fetchJson<LanguageInfo[]>('/api/v1/platform/languages');
    write(STORAGE_LANGUAGES, languages);
  } catch { /* keep the cached list */ }
  if (languages.length === 0)
    languages = [{ code: 'en', name: { en: 'English' }, direction: 'ltr', isEnabled: true, isDefault: true, sortOrder: 0 }];

  const code = pick(read<string>(STORAGE_LANG));
  try {
    adopt(await loadCatalog(code));
  } catch {
    current = code;
    defaultLanguage = languages.find(l => l.isDefault)?.code ?? code;
    apply(languages.find(l => l.code === code)?.direction ?? 'ltr');
  }
}

/** The reader's language code. */
export function getLang(): string {
  return current;
}

/** The platform's default language (every text has it). */
export function getDefaultLang(): string {
  return defaultLanguage;
}

/** The enabled languages, in display order. */
export function getLanguages(): LanguageInfo[] {
  return languages.filter(l => l.isEnabled).sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
}

/** The direction of the reader's language (or of another enabled language). */
export function dirOf(code: string = current): 'ltr' | 'rtl' {
  return languages.find(l => l.code === code)?.direction ?? 'ltr';
}

/** Switches the language: loads its catalogue, then re-renders and flips the layout if its direction differs. */
export async function setLang(code: string): Promise<void> {
  if (code === current) return;
  write(STORAGE_LANG, code);
  adopt(await loadCatalog(code));
}

/** Drops the browser's cached catalogues (after staff edit texts) and reloads the current one. */
export async function refreshI18n(): Promise<void> {
  languages.forEach(l => { try { localStorage.removeItem(storageCatalog(l.code)); } catch { /* ignore */ } });
  await initI18n();
}

/** Re-renders the caller when the language or its texts change. Each app calls it once at the root. */
export function useLang(): string {
  useSyncExternalStore(
    cb => { listeners.add(cb); return () => listeners.delete(cb); },
    () => version,
  );
  return current;
}

/** A multilingual value in the reader's language, else the default language, else any language it has. */
export function loc(text: LocalizedText | null | undefined): string;
export function loc(text: LocalizedText | null | undefined, fallback: string): string;
export function loc(text: LocalizedText | null | undefined, fallback = ''): string {
  if (!text) return fallback;
  return text[current] || text[current.split('-')[0]] || text[defaultLanguage]
    || Object.values(text).find(v => v && v.trim()) || fallback;
}

type Vars = Record<string, string | number>;

/** A text by key in the reader's language, with {placeholders} filled. A key with no text is shown as is. */
export function t(key: TKey, vars?: Vars): string {
  let text = strings[key] ?? key;
  if (vars) for (const [k, v] of Object.entries(vars)) text = text.split(`{${k}}`).join(String(v));
  return text;
}

/** True when the catalogue has a text for the key (used to show server message keys, e.g. sign-in errors). */
export function hasText(key: string): boolean {
  return key in strings;
}

/** The site's name for a portal: "<portal> | <ministry>" (both parts and the pattern are translatable texts). */
export function siteName(service: string): string {
  return t('site.name', { service, ministry: t('entity.name') });
}
