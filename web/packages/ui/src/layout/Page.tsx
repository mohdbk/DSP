import type { ReactNode } from 'react';
import { AegovCard } from '@aegov/designsystem-react';

/** The heading block at the top of every page: optional overline, the page's one h1, badges and actions. */
export function PageHeader({ overline, title, intro, badges, actions }: {
  overline?: ReactNode;
  title: ReactNode;
  intro?: ReactNode;
  badges?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div className="max-w-3xl">
        {overline && <div className="text-sm text-aeblack-500">{overline}</div>}
        <h1 className="text-h3 font-bold text-aeblack-900">{title}</h1>
        {intro && <p className="mt-1 text-aeblack-600">{intro}</p>}
        {badges && <div className="mt-2 flex flex-wrap gap-2">{badges}</div>}
      </div>
      {actions && <div className="flex flex-wrap gap-3">{actions}</div>}
    </div>
  );
}

/** A titled content panel (AEGOV card). Every boxed section of both portals uses it. */
export function Panel({ title, actions, children, className }: {
  title?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <AegovCard appearance="solid" variant="neutral-min" borderWidth={1} className={className}>
      {(title || actions) && (
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          {title && <h2 className="text-h6 font-bold text-aeblack-900">{title}</h2>}
          {actions}
        </div>
      )}
      {children}
    </AegovCard>
  );
}

/** Main content with a side column (stacks on small screens). */
export function SplitLayout({ main, aside }: { main: ReactNode; aside: ReactNode }) {
  return (
    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div className="flex min-w-0 flex-col gap-6">{main}</div>
      <aside className="flex flex-col gap-6">{aside}</aside>
    </div>
  );
}

/** Evenly sized cards that wrap. */
export function CardGrid({ children }: { children: ReactNode }) {
  return <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-3">{children}</div>;
}

/** A row of buttons at the end of a form or panel. */
export function Actions({ children, align = 'end' }: { children: ReactNode; align?: 'start' | 'end' | 'between' }) {
  const justify = align === 'start' ? 'justify-start' : align === 'between' ? 'justify-between' : 'justify-end';
  return <div className={`mt-6 flex flex-wrap items-center gap-3 ${justify}`}>{children}</div>;
}
