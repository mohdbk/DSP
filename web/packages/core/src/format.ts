import { getLang } from './i18n';

// UAE Design System content rules: dates as "11 November 2026", amounts as "AED 1,500".
// Arabic keeps Latin digits, as UAE government services do.

function locale() {
  return getLang() === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB';
}

export function formatDate(value?: string | null, withTime = false): string {
  if (!value) return '';
  const date = new Date(value);
  const options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'long', year: 'numeric' };
  if (withTime) Object.assign(options, { hour: '2-digit', minute: '2-digit' });
  return date.toLocaleString(locale(), options);
}

/** "AED 1,500" in both languages; render it in a left-to-right run so the code stays before the number. */
export function formatMoney(amount: number, currency: string): string {
  const value = amount.toLocaleString('en-AE', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
  return `${currency} ${value}`;
}
