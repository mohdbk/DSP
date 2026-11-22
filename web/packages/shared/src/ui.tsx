import type { ReactNode } from 'react';
import type { ApplicationStatus } from './types';

const statusLabels: Record<ApplicationStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  AwaitingPayment: 'Awaiting payment',
  Paid: 'Paid',
  Approved: 'Completed',
  Rejected: 'Rejected',
};

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  return <span className={`badge badge-${status}`}>{statusLabels[status] ?? status}</span>;
}

export const statusLabel = (s: ApplicationStatus) => statusLabels[s] ?? s;

export function formatDate(value?: string | null, withTime = false) {
  if (!value) return '';
  const d = new Date(value);
  return withTime
    ? d.toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
    : d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function formatMoney(amount: number, currency: string) {
  return `${amount.toLocaleString('en-AE', { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${currency}`;
}

export function Alert({ kind = 'info', children }: { kind?: 'info' | 'error' | 'success' | 'warning'; children: ReactNode }) {
  return <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>{children}</div>;
}

export function Spinner({ label = 'Loading…' }: { label?: string }) {
  return <div className="spinner" role="status"><span className="dot" />{label}</div>;
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="empty"><h3>{title}</h3>{children}</div>;
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
        <div className="header-right">{right}</div>
      </div>
    </header>
  );
}

export function Footer() {
  return (
    <footer className="app-footer">
      <div>Ministry of Economy and Tourism · Digital Transformation</div>
      <div className="muted">Digital Services Platform · proof of concept</div>
    </footer>
  );
}
