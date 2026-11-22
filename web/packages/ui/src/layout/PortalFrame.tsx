import { useEffect, useState, Fragment } from 'react';
import { Outlet, useRouter, useRouterState } from '@tanstack/react-router';
import { siteName, useLang } from '@dsp/core';
import { AppShell, type NavItem } from './AppShell';
import { AccountArea } from '../navigation/AccountArea';
import { useInternalLinks } from '../navigation/useInternalLinks';

/**
 * The root route component of a portal: the government shell, its navigation, the signed-in user with their
 * unread notifications, language switching and in-app link handling. Each portal only supplies its settings.
 */
export function PortalFrame({ service, homeLabel, navItems, user, notificationsHref, loadUnread, onSignOut, publicPaths }: {
  service: string;
  /** The footer link back to the portal's home page (e.g. "E-Services home"). */
  homeLabel: string;
  /** The navigation for the current path (shown only when signed in). */
  navItems: (pathname: string) => NavItem[];
  user: { name: string; role: string } | null;
  notificationsHref?: string;
  loadUnread: () => Promise<number>;
  onSignOut: () => void;
  /** Paths that show the bare shell (sign-in pages). */
  publicPaths: string[];
}) {
  const lang = useLang(); // re-render everything when the language changes
  useEffect(() => { document.title = siteName(service); }, [service, lang]);
  const router = useRouter();
  useInternalLinks(href => router.history.push(href));
  const pathname = useRouterState({ select: s => s.location.pathname });
  const [unread, setUnread] = useState(0);
  const signedIn = !!user && !publicPaths.some(p => pathname.startsWith(p));

  useEffect(() => {
    if (signedIn) loadUnread().then(setUnread).catch(() => {});
  }, [pathname, signedIn]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <AppShell service={service} homeLabel={homeLabel} nav={signedIn ? navItems(pathname) : []}
      account={signedIn && user ? (
        <AccountArea name={user.name} role={user.role} unread={unread} notificationsHref={notificationsHref} onSignOut={onSignOut} />
      ) : undefined}>
      {/* TanStack Router memoises route components, so they wouldn't see the new language: remount the page. */}
      <div className="contents"><Fragment key={lang}><Outlet /></Fragment></div>
    </AppShell>
  );
}
