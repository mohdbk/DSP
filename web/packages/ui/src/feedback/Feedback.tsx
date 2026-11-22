import type { ReactNode } from 'react';
import { AegovBadge, AegovCallout, AegovProgress } from '@aegov/designsystem-react';
import { t, type ApplicationStatus, type TKey } from '@dsp/core';

type CalloutVariant = 'default' | 'error' | 'info' | 'success' | 'warning';

/** An inline message (AEGOV callout). */
export function Notice({ kind = 'info', title, children, actions }: {
  kind?: CalloutVariant;
  title?: ReactNode;
  children?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <AegovCallout variant={kind} className="mb-4 block">
      {title && <p className="font-bold">{title}</p>}
      {children}
      {actions && <div slot="actions" className="flex flex-wrap gap-3">{actions}</div>}
    </AegovCallout>
  );
}

/** Work in progress: an indeterminate AEGOV progress bar (the design system has no spinner). */
export function Loading({ label }: { label?: string }) {
  const text = label ?? t('loading');
  return (
    <div className="py-8" role="status" aria-live="polite">
      <AegovProgress label={text} />
      <p className="mt-2 text-sm text-aeblack-600">{text}</p>
    </div>
  );
}

/** Nothing to show yet. */
export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="py-12 text-center">
      <p className="text-h6 font-bold text-aeblack-800">{title}</p>
      {children && <div className="mt-2 text-aeblack-600">{children}</div>}
    </div>
  );
}

type BadgeVariant = 'aeblack' | 'aegold' | 'aegreen' | 'aered' | 'camel' | 'default' | 'desert' | 'fuchsia' | 'indigo'
  | 'lime' | 'pink' | 'primary' | 'purple' | 'seablue' | 'secondary' | 'slate' | 'techblue';

/** A short label (AEGOV badge). */
export function Tag({ variant = 'default', children }: { variant?: BadgeVariant; children: ReactNode }) {
  return <AegovBadge variant={variant}>{children}</AegovBadge>;
}

const statusVariants: Record<ApplicationStatus, BadgeVariant> = {
  Draft: 'slate',
  Submitted: 'seablue',
  AiReview: 'indigo',
  InformationRequested: 'camel',
  UnderReview: 'techblue',
  AwaitingPayment: 'aegold',
  Paid: 'aegold',
  Approved: 'aegreen',
  Rejected: 'aered',
};

/** An application's status, in words (never colour alone). */
export function StatusBadge({ status }: { status: ApplicationStatus }) {
  return <Tag variant={statusVariants[status] ?? 'default'}>{t(`status.${status}` as TKey)}</Tag>;
}

/** An amount, kept left-to-right so "AED" stays before the number in Arabic too. */
export function Money({ children }: { children: string }) {
  return <span dir="ltr">{children}</span>;
}
