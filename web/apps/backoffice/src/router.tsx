import { createRootRoute, createRoute, createRouter, redirect } from '@tanstack/react-router';
import { countUnread, t, useLang } from '@dsp/core';
import {
  EmptyState, Files, GearSix, PortalFrame, requireSignedIn, Tray, validateListSearch, Wrench,
} from '@dsp/ui';
import { api, currentUser, signOut, useCurrentUser } from './session';
import { LoginPage } from './pages/Login';
import { InboxPage } from './pages/Inbox';
import { TaskPage } from './pages/Task';
import { ApplicationDetailPage, ApplicationsPage } from './pages/Applications';
import { ServicesPage } from './pages/builder/Services';
import { ServiceEditorPage } from './pages/builder/ServiceEditor';
import { LanguagesPage } from './pages/settings/Languages';
import { TranslationsPage } from './pages/settings/Translations';
import { AiSettingsPage } from './pages/settings/AiSettings';
import { CurrenciesPage } from './pages/settings/Currencies';

/** The back-office frame: the internal staff shell (side navigation, no public header), and the signed-in officer. */
function Root() {
  useLang(); // the portal name and labels below follow the language
  const user = useCurrentUser(); // re-renders the frame (and its menu) right after sign-in
  return (
    <PortalFrame layout="staff" service={t('app.backoffice')} homeLabel={t('app.backofficeHome')} publicPaths={['/login']} onSignOut={signOut}
      user={user && { name: user.name, role: user.title ?? t('staff') }}
      loadUnread={() => countUnread(api)}
      navItems={pathname => [
        { href: '/inbox', label: t('nav.inbox'), active: pathname.startsWith('/inbox') || pathname.startsWith('/tasks'), icon: Tray },
        { href: '/applications', label: t('nav.allApplications'), active: pathname.startsWith('/applications'), icon: Files },
        { href: '/services', label: t('nav.builder'), active: pathname.startsWith('/services'), icon: Wrench },
        {
          href: '/settings/languages', label: t('nav.settings'), active: pathname.startsWith('/settings'), icon: GearSix,
          children: [
            { href: '/settings/languages', label: t('settings.languages'), active: pathname.startsWith('/settings/languages') },
            { href: '/settings/translations', label: t('settings.translations'), active: pathname.startsWith('/settings/translations') },
            { href: '/settings/currencies', label: t('settings.currencies'), active: pathname.startsWith('/settings/currencies') },
            { href: '/settings/ai', label: t('settings.ai'), active: pathname.startsWith('/settings/ai') },
          ],
        },
      ]} />
  );
}

const guard = requireSignedIn(() => !!currentUser(), '/login');

const rootRoute = createRootRoute({
  component: Root,
  notFoundComponent: () => <EmptyState title={t('app.notFound')} />,
});

const indexRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/', beforeLoad: () => { throw redirect({ to: '/inbox' }); },
});
const loginRoute = createRoute({ getParentRoute: () => rootRoute, path: '/login', component: LoginPage });
const inboxRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/inbox', beforeLoad: guard, validateSearch: validateListSearch, component: InboxPage,
});
const taskRoute = createRoute({ getParentRoute: () => rootRoute, path: '/tasks/$id', beforeLoad: guard, component: TaskPage });
const applicationsRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/applications', beforeLoad: guard, validateSearch: validateListSearch,
  component: ApplicationsPage,
});
const applicationRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/applications/$id', beforeLoad: guard, component: ApplicationDetailPage,
});
const servicesRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/services', beforeLoad: guard, validateSearch: validateListSearch, component: ServicesPage,
});
const newServiceRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/services/new', beforeLoad: guard, validateSearch: validateListSearch,
  component: () => <ServicesPage creating />,
});
const serviceRoute = createRoute({ getParentRoute: () => rootRoute, path: '/services/$code', beforeLoad: guard, component: ServiceEditorPage });

const settingsRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/settings', beforeLoad: () => { throw redirect({ to: '/settings/languages' }); },
});
const languagesRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/settings/languages', beforeLoad: guard, validateSearch: validateListSearch,
  component: LanguagesPage,
});
const translationsRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/settings/translations', beforeLoad: guard, validateSearch: validateListSearch,
  component: TranslationsPage,
});
const currenciesRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/settings/currencies', beforeLoad: guard, validateSearch: validateListSearch,
  component: CurrenciesPage,
});
const aiSettingsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/settings/ai', beforeLoad: guard, component: AiSettingsPage });

export const router = createRouter({
  routeTree: rootRoute.addChildren([
    indexRoute, loginRoute, inboxRoute, taskRoute, applicationsRoute, applicationRoute, servicesRoute, newServiceRoute, serviceRoute,
    settingsRoute, languagesRoute, translationsRoute, currenciesRoute, aiSettingsRoute,
  ]),
});

declare module '@tanstack/react-router' {
  interface Register { router: typeof router }
}
