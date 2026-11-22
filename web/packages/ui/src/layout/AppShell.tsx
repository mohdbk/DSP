import type { ReactNode } from 'react';
import { AegovFooter, AegovHeader, AegovNavbar, AegovNavbarItem } from '@aegov/designsystem-react';
import { getLang, siteName, t } from '@dsp/core';
import { LanguageSwitch } from '../navigation/LanguageSwitch';
import { Icon } from '../icons';
import type { Icon as PhosphorIcon } from '@phosphor-icons/react';
import logoEn from '../assets/moet-logo-en.png';
import logoAr from '../assets/moet-logo-ar.png';

/** The entity's public website, used by the official header's logo link and top bar. Confirm before go-live. */
export const ENTITY_DOMAIN = 'https://www.moet.gov.ae';

/** The ministry's rating in the Global Star Rating System for Services (shown by the official header badge). */
export const GSR_STARS = 4;

export interface NavItem {
  href: string;
  label: string;
  active?: boolean;
  icon?: PhosphorIcon;
  /** Sub-pages (staff side navigation only), shown under the item while it is active. */
  children?: NavItem[];
}

/**
 * The page frame of both portals: the mandatory AEGOV government header and footer around the page.
 * The navigation and account area are repeated in the small-screen menu, as the header requires.
 */
export function AppShell({ service, homeLabel, nav = [], account, children }: {
  /** The portal's name, shown under the logo area for orientation. */
  service: string;
  /** The footer link back to the portal's home page. */
  homeLabel: string;
  nav?: NavItem[];
  /** Account area (user, notifications, sign out); omitted on sign-in pages. */
  account?: ReactNode;
  children: ReactNode;
}) {
  // The header moves its slotted children into its own layout, so React must never add or remove them in place:
  // when the shape changes (signed in or out), the key gives React a fresh header instead.
  const shape = `${nav.length > 0 ? 'nav' : 'bare'}-${account ? 'account' : 'anonymous'}`;
  // The official MoET lockup in the page language. The header and footer mirror for right-to-left as AEGOV specifies.
  const logo = getLang() === 'ar' ? logoAr : logoEn;
  return (
    <div className="flex min-h-screen flex-col bg-whitely-50">
      <AegovHeader key={shape} gsrStars={GSR_STARS} entityName={t('entity.name')} entityDomain={ENTITY_DOMAIN} logoLabel={t('entity.home')}
        skipContentContainer="#main" labelMenu={t('nav.menu')}>
        {/* The header sizes its logo with ::slotted(img), but Tailwind's base styles (height: auto; max-width: 100%) win
            over shadow styles, so the same AEGOV sizes are set here: 48/64/72px tall, at most 12/18/20rem wide. */}
        <img slot="logo" src={logo} alt={t('entity.name')} className="h-12 w-auto max-w-48 lg:h-16 lg:max-w-72 xl:h-18 xl:max-w-80" />
        <div slot="actions"><LanguageSwitch onDark /></div>
        {nav.length > 0 && (
          <AegovNavbar slot="navigation" labelNav={t('nav.main')}>
            {nav.map(item => (
              <AegovNavbarItem key={item.href} href={item.href} isActive={!!item.active}>
                {/* Always rendered: AEGOV moves slotted nodes, so React must not add or remove them. */}
                <span slot="icon-start" className={item.icon ? 'inline-flex' : 'hidden'}>
                  {item.icon && <Icon icon={item.icon} weight={item.active ? 'fill' : 'regular'} />}
                </span>
                {item.label}
              </AegovNavbarItem>
            ))}
          </AegovNavbar>
        )}
        {account && <div slot="secondary-actions" className="flex items-center gap-3">{account}</div>}
        <nav slot="mobile-content" aria-label={t('nav.main')} className="flex flex-col gap-4">
          <p className="text-sm font-semibold text-aeblack-600">{siteName(service)}</p>
          {nav.length > 0 && (
            <ul className="m-0 flex list-none flex-col gap-2 p-0">
              {nav.map(item => (
                <li key={item.href}>
                  <a href={item.href} aria-current={item.active ? 'page' : undefined} className="inline-flex items-center gap-2">
                    {item.icon && <Icon icon={item.icon} weight={item.active ? 'fill' : 'regular'} />}
                    {item.label}
                  </a>
                </li>
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
          <img src={logo} alt={t('entity.name')} className="h-14 w-auto" />
          <p className="mt-4 text-sm text-aeblack-600">{t('footer.about')}</p>
        </div>
        <div className="aegov-footer__group">
          <h2>{service}</h2>
          <ul role="list">
            <li><a href="/">{homeLabel}</a></li>
          </ul>
        </div>
        <p slot="copyright">© {new Date().getFullYear()} {t('entity.name')}. {t('footer.rights')}</p>
      </AegovFooter>
    </div>
  );
}
