import type { ReactNode } from 'react';
import { AegovFooter, AegovHeader, AegovNavbar, AegovNavbarItem } from '@aegov/designsystem-react';
import { t } from '@dsp/core';
import { LanguageSwitch } from '../navigation/LanguageSwitch';
import logoUrl from '../assets/logo.svg';

/** The entity's public website, used by the official header's logo link and top bar. Confirm before go-live. */
export const ENTITY_DOMAIN = 'https://www.moet.gov.ae';

export interface NavItem { href: string; label: string; active?: boolean }

/**
 * The page frame of both portals: the mandatory AEGOV government header and footer around the page.
 * The navigation and account area are repeated in the small-screen menu, as the header requires.
 */
export function AppShell({ service, nav = [], account, children }: {
  /** The portal's name, shown under the logo area for orientation. */
  service: string;
  nav?: NavItem[];
  /** Account area (user, notifications, sign out); omitted on sign-in pages. */
  account?: ReactNode;
  children: ReactNode;
}) {
  // The header moves its slotted children into its own layout, so React must never add or remove them in place:
  // when the shape changes (signed in or out), the key gives React a fresh header instead.
  const shape = `${nav.length > 0 ? 'nav' : 'bare'}-${account ? 'account' : 'anonymous'}`;
  return (
    <div className="flex min-h-screen flex-col bg-whitely-50">
      <AegovHeader key={shape} entityName={t('entity.name')} entityDomain={ENTITY_DOMAIN} logoLabel={t('entity.home')}
        skipContentContainer="#main" labelMenu={t('nav.menu')}>
        <img slot="logo" src={logoUrl} alt={t('entity.name')} />
        <div slot="actions"><LanguageSwitch /></div>
        {nav.length > 0 && (
          <AegovNavbar slot="navigation" labelNav={t('nav.main')}>
            {nav.map(item => <AegovNavbarItem key={item.href} href={item.href} isActive={!!item.active}>{item.label}</AegovNavbarItem>)}
          </AegovNavbar>
        )}
        {account && <div slot="secondary-actions" className="flex items-center gap-3">{account}</div>}
        <nav slot="mobile-content" aria-label={t('nav.main')} className="flex flex-col gap-4">
          <p className="text-sm font-semibold text-aeblack-600">{service}</p>
          {nav.length > 0 && (
            <ul className="m-0 flex list-none flex-col gap-2 p-0">
              {nav.map(item => (
                <li key={item.href}><a href={item.href} aria-current={item.active ? 'page' : undefined}>{item.label}</a></li>
              ))}
            </ul>
          )}
          {account && <div className="flex flex-col items-start gap-3">{account}</div>}
          <LanguageSwitch />
        </nav>
      </AegovHeader>

      <main id="main" className="aegov-container flex-1 py-8">{children}</main>

      <AegovFooter navVariant="flat">
        <div slot="brand">
          <img src={logoUrl} alt={t('entity.name')} className="h-14 w-auto" />
          <p className="mt-4 text-sm text-aeblack-600">{t('footer.about')}</p>
        </div>
        <div className="aegov-footer__group">
          <h2>{t('footer.services')}</h2>
          <ul role="list">
            <li><a href="/">{service}</a></li>
          </ul>
        </div>
        <p slot="copyright">© {new Date().getFullYear()} {t('entity.name')}. {t('footer.rights')} · {t('footer.platform')}</p>
      </AegovFooter>
    </div>
  );
}
