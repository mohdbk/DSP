import { useState, type ReactNode } from 'react';
import { AegovDrawer } from '@aegov/designsystem-react';
import { dirOf, getLang, t } from '@dsp/core';
import { LanguageSwitch } from '../navigation/LanguageSwitch';
import { Icon, List } from '../icons';
import type { NavItem } from './AppShell';
import logoEn from '../assets/moet-logo-en.png';
import logoAr from '../assets/moet-logo-ar.png';

/**
 * The frame of internal staff applications (the back office). Not a public government website, so it has no
 * official header banner, star-rating badge or public footer: a side navigation with icons, a slim app bar with the
 * account and language, and the AEGOV tokens throughout. Below `lg` the navigation opens in an AEGOV drawer.
 */
export function StaffShell({ service, nav = [], account, children }: {
  service: string;
  nav?: NavItem[];
  account?: ReactNode;
  children: ReactNode;
}) {
  const [menuOpen, setMenuOpen] = useState(false);
  const logo = getLang() === 'ar' ? logoAr : logoEn;
  const hasNav = nav.length > 0;

  return (
    <div className="flex min-h-screen bg-whitely-50">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-white focus:px-4 focus:py-2">
        {t('nav.skip')}
      </a>

      {hasNav && (
        <aside className="sticky top-0 hidden h-screen w-72 shrink-0 flex-col border-e border-aeblack-100 bg-white lg:flex">
          <div className="border-b border-aeblack-100 px-6 py-5">
            <img src={logo} alt={t('entity.name')} className="h-12 w-auto max-w-56" />
          </div>
          <div className="flex-1 overflow-y-auto px-3 py-4"><SideNav items={nav} /></div>
        </aside>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex items-center justify-between gap-3 border-b border-aeblack-100 bg-white px-4 py-3 lg:px-8">
          <div className="flex min-w-0 items-center gap-3">
            {hasNav && (
              <button type="button" aria-label={t('nav.menu')} title={t('nav.menu')} onClick={() => setMenuOpen(true)}
                className="flex size-10 items-center justify-center rounded-lg text-aeblack-800 hover:bg-aeblack-50 focus-visible:outline-2 focus-visible:outline-primary-600 lg:hidden">
                <Icon icon={List} />
              </button>
            )}
            <img src={logo} alt={t('entity.name')} className={`h-10 w-auto max-w-40 ${hasNav ? 'lg:hidden' : ''}`} />
            <span className="truncate font-semibold text-aeblack-900">{service}</span>
          </div>
          <div className="flex items-center gap-4">
            {account && <div className="hidden items-center gap-4 lg:flex">{account}</div>}
            <LanguageSwitch />
          </div>
        </header>

        <main id="main" className="w-full max-w-screen-2xl flex-1 px-4 py-8 lg:px-8">{children}</main>

        <footer className="border-t border-aeblack-100 px-4 py-4 text-sm text-aeblack-600 lg:px-8">
          © {new Date().getFullYear()} {t('entity.name')}. {t('footer.rights')}
        </footer>
      </div>

      {hasNav && (
        <AegovDrawer isOpen={menuOpen} position={dirOf(getLang()) === 'rtl' ? 'right' : 'left'} size="xs" label={t('nav.main')}
          onAegovDrawerClose={() => setMenuOpen(false)}>
          {/* One stable child: the drawer may move its content, so React only changes what is inside. */}
          <div className="flex flex-col gap-6">
            <img src={logo} alt={t('entity.name')} className="h-12 w-auto max-w-56" />
            <SideNav items={nav} onNavigate={() => setMenuOpen(false)} />
            {account && <div className="flex flex-col items-start gap-3 border-t border-aeblack-100 pt-4">{account}</div>}
          </div>
        </AegovDrawer>
      )}
    </div>
  );
}

/** Vertical navigation: icon and label per item; an active item's sub-items show under it. */
function SideNav({ items, onNavigate }: { items: NavItem[]; onNavigate?: () => void }) {
  const link = (item: NavItem, nested: boolean) => (
    <a href={item.href} aria-current={item.active ? 'page' : undefined} onClick={onNavigate}
      className={`flex items-center gap-3 rounded-lg px-3 py-2 ${nested ? 'ps-11 text-sm' : ''} ${item.active
        ? 'bg-primary-50 font-semibold text-primary-700'
        : 'text-aeblack-700 hover:bg-aeblack-50 hover:text-aeblack-900'} focus-visible:outline-2 focus-visible:outline-primary-600`}>
      {!nested && item.icon && <Icon icon={item.icon} weight={item.active ? 'fill' : 'regular'} />}
      <span>{item.label}</span>
    </a>
  );
  return (
    <nav aria-label={t('nav.main')}>
      <ul className="m-0 flex list-none flex-col gap-1 p-0">
        {items.map(item => (
          <li key={item.href}>
            {link({ ...item, active: item.active && !item.children?.some(c => c.active) }, false)}
            {item.children && item.active && (
              <ul className="m-0 mt-1 flex list-none flex-col gap-1 p-0">
                {item.children.map(child => <li key={child.href}>{link(child, true)}</li>)}
              </ul>
            )}
          </li>
        ))}
      </ul>
    </nav>
  );
}
