import type { LocalizedText } from './i18n';

// Currencies from the Platform service (Settings → Currencies): how amounts are written and rounded.
// Loaded once at start-up next to the languages, and cached so the last list is used when offline.

export interface CurrencyInfo {
  code: string;
  name: LocalizedText;
  symbol: string;
  symbolPosition: 'before' | 'after';
  decimals: number;
  isEnabled: boolean;
  isDefault: boolean;
  sortOrder: number;
}

const STORAGE = 'dsp.currencies';
let currencies: CurrencyInfo[] = (() => {
  try { return JSON.parse(localStorage.getItem(STORAGE) ?? '[]') as CurrencyInfo[]; } catch { return []; }
})();

export async function initCurrencies(): Promise<void> {
  try {
    const response = await fetch('/api/v1/platform/currencies', { headers: { Accept: 'application/json' } });
    if (!response.ok) return;
    currencies = await response.json() as CurrencyInfo[];
    try { localStorage.setItem(STORAGE, JSON.stringify(currencies)); } catch { /* storage unavailable */ }
  } catch { /* keep the cached list */ }
}

/** Every currency, in display order (disabled ones too, so old amounts still format). */
export function getCurrencies(): CurrencyInfo[] {
  return [...currencies].sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
}

/** The currency for a code; an unknown code is written as itself with two decimals. */
export function getCurrency(code: string): CurrencyInfo {
  return currencies.find(c => c.code === code)
    ?? { code, name: { en: code }, symbol: code, symbolPosition: 'before', decimals: 2, isEnabled: true, isDefault: false, sortOrder: 0 };
}

/** Rounds to the currency's minor unit, half away from zero. */
export function roundMoney(amount: number, code: string): number {
  const factor = 10 ** getCurrency(code).decimals;
  return Math.sign(amount) * Math.round(Math.abs(amount) * factor) / factor;
}
