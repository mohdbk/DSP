import { getLang } from './i18n';
import { getCurrency, roundMoney } from './currencies';

// UAE Design System content rules: dates as "11 November 2026", amounts as "AED 1,500".
// Every language keeps Latin digits, as UAE government services do.

function locale() {
  const lang = getLang();
  return lang === 'en' ? 'en-GB' : `${lang === 'ar' ? 'ar-AE' : lang}-u-nu-latn`;
}

export function formatDate(value?: string | null, withTime = false): string {
  if (!value) return '';
  const date = new Date(value);
  const options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'long', year: 'numeric' };
  if (withTime) Object.assign(options, { hour: '2-digit', minute: '2-digit' });
  return date.toLocaleString(locale(), options);
}

/**
 * "AED 1,500" or "AED 1,500.50" in every language, with the currency's symbol, position and decimals (Settings →
 * Currencies). Whole amounts drop the decimals. Render it in a left-to-right run (Money) so it reads the same in RTL.
 */
export function formatMoney(amount: number, currency: string): string {
  const info = getCurrency(currency);
  const rounded = roundMoney(amount, currency);
  const digits = Number.isInteger(rounded) ? 0 : info.decimals;
  const value = rounded.toLocaleString('en-AE', { minimumFractionDigits: digits, maximumFractionDigits: digits });
  return info.symbolPosition === 'after' ? `${value} ${info.symbol}` : `${info.symbol} ${value}`;
}
