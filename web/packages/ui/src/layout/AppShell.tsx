import type { ReactNode } from 'react';
import { AegovFooter, AegovHeader, AegovNavbar, AegovNavbarItem } from '@aegov/designsystem-react';
import { getLang, t } from '@dsp/core';
import { LanguageSwitch } from '../navigation/LanguageSwitch';
import logoEn from '../assets/moet-logo-en.png';
import logoAr from '../assets/moet-logo-ar.png';

/** The entity's public website, used by the official header's logo link and top bar. Confirm before go-live. */
export const ENTITY_DOMAIN = 'https://www.moet.gov.ae';

/** The ministry's rating in the Global Star Rating System for Services (shown by the official header badge). */
export const GSR_STARS = 4;

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
  // Ministry identity: the logo (emblem first) always sits on the left, in every language. The header and footer
  // mirror for right-to-left, so they are laid out left to right and their content is set back to the page direction.
  const lang = getLang();
  const dir = document.documentElement.dir === 'rtl' ? 'rtl' : 'ltr';
  const logo = lang === 'ar' ? logoAr : logoEn;
  return (
    <div className="flex min-h-screen flex-col bg-whitely-50">
      <div dir="ltr">
      <AegovHeader key={`${shape}-${dir}`} gsrStars={GSR_STARS} entityName={t('entity.name')} entityDomain={ENTITY_DOMAIN} logoLabel={t('entity.home')}
        skipContentContainer="#main" labelMenu={t('nav.menu')}>
        <img slot="logo" src={logo} alt={t('entity.name')} />
        <div slot="actions" dir={dir}><LanguageSwitch /></div>
        {nav.length > 0 && (
          <AegovNavbar slot="navigation" dir={dir} labelNav={t('nav.main')}>
            {nav.map(item => <AegovNavbarItem key={item.href} href={item.href} isActive={!!item.active}>{item.label}</AegovNavbarItem>)}
          </AegovNavbar>
        )}
        {account && <div slot="secondary-actions" dir={dir} className="flex items-center gap-3">{account}</div>}
        <nav slot="mobile-content" dir={dir} aria-label={t('nav.main')} className="flex flex-col gap-4">
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
      </div>

      <main id="main" className="aegov-container flex-1 py-8">{children}</main>

      <div dir="ltr">
      <AegovFooter key={dir} navVariant="flat">
        <div slot="brand">
          <img src={logo} alt={t('entity.name')} className="h-14 w-auto" />
          <p dir={dir} className="mt-4 text-start text-sm text-aeblack-600">{t('footer.about')}</p>
        </div>
        <div className="aegov-footer__group" dir={dir}>
          <h2>{t('footer.services')}</h2>
          <ul role="list">
            <li><a href="/">{service}</a></li>
          </ul>
        </div>
        <p slot="copyright" dir={dir}>© {new Date().getFullYear()} {t('entity.name')}. {t('footer.rights')} · {t('footer.platform')}</p>
      </AegovFooter>
      </div>
    </div>
  );
}
