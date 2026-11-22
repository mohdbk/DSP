import type { ReactNode } from 'react';
import type { ApplicationStatus } from './types';
import { getLang, setLang, t, type TKey } from './i18n';

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  return <span className={`badge badge-${status}`}>{statusLabel(status)}</span>;
}

export const statusLabel = (s: ApplicationStatus) => t(`status.${s}` as TKey);

/** Dates in the UI language. Arabic keeps Latin digits, as UAE government services do. */
export function formatDate(value?: string | null, withTime = false) {
  if (!value) return '';
  const d = new Date(value);
  const locale = getLang() === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB';
  return withTime
    ? d.toLocaleString(locale, { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
    : d.toLocaleDateString(locale, { day: '2-digit', month: 'short', year: 'numeric' });
}

export function formatMoney(amount: number, currency: string) {
  const value = amount.toLocaleString('en-AE', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
  return getLang() === 'ar' && currency === 'AED' ? `${value} درهم` : `${value} ${currency}`;
}

export function Alert({ kind = 'info', children }: { kind?: 'info' | 'error' | 'success' | 'warning'; children: ReactNode }) {
  return <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>{children}</div>;
}

export function Spinner({ label }: { label?: string }) {
  return <div className="spinner" role="status"><span className="dot" />{label ?? t('loading')}</div>;
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="empty"><h3>{title}</h3>{children}</div>;
}

/** Switches between English and Arabic; the layout flips to right-to-left. */
export function LanguageSwitch() {
  const next = getLang() === 'ar' ? 'en' : 'ar';
  return (
    <button type="button" className="btn btn-ghost btn-sm lang-switch" lang={next} aria-label={t('lang.switchLabel')}
      onClick={() => setLang(next)}>
      {t('lang.switch')}
    </button>
  );
}

/** Ministry header bar shared by both portals. */
export function Header({ title, subtitle, nav, right }: { title: string; subtitle?: string; nav?: ReactNode; right?: ReactNode }) {
  return (
    <header className="app-header">
      <div className="header-inner">
        <div className="brand">
          <div className="brand-mark" aria-hidden="true">م</div>
          <div>
            <div className="brand-title">{title}</div>
            {subtitle && <div className="brand-sub">{subtitle}</div>}
          </div>
        </div>
        {nav && <nav className="main-nav">{nav}</nav>}
        <div className="header-right">{right}<LanguageSwitch /></div>
      </div>
    </header>
  );
}

export function Footer() {
  return (
    <footer className="app-footer">
      <div>{t('footer.ministry')}</div>
      <div className="muted">{t('footer.platform')}</div>
    </footer>
  );
}
