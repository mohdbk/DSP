import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router';
import { countUnread, t } from '@dsp/core';
import { EmptyState, PortalFrame, requireSignedIn, validateListSearch } from '@dsp/ui';
import { api, currentUser, signOut } from './session';
import { AuthCallbackPage, LoginPage } from './pages/Login';
import { CatalogPage } from './pages/Catalog';
import { MyApplicationsPage } from './pages/MyApplications';
import { ApplicationPage } from './pages/Application';
import { NotificationsPage } from './pages/Notifications';

/** The portal frame: government header and footer, navigation and the signed-in citizen. */
function Root() {
  const user = currentUser();
  return (
    <PortalFrame service={t('app.portal')} homeLabel={t('app.portalHome')} publicPaths={['/login', '/auth']} onSignOut={signOut}
      user={user && { name: user.name, role: user.accountLevel ? `${t('uaePass')} · ${user.accountLevel}` : t('uaePass') }}
      notificationsHref="/notifications"
      loadUnread={() => countUnread(api)}
      navItems={pathname => [
        { href: '/', label: t('nav.services'), active: pathname === '/' },
        { href: '/applications', label: t('nav.myApplications'), active: pathname.startsWith('/applications') },
      ]} />
  );
}

const guard = requireSignedIn(() => !!currentUser(), '/login');

const rootRoute = createRootRoute({
  component: Root,
  notFoundComponent: () => <EmptyState title={t('app.notFound')} />,
});

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/login',
  validateSearch: (s: Record<string, unknown>): { returnUrl?: string; error?: string } => ({
    returnUrl: typeof s.returnUrl === 'string' ? s.returnUrl : undefined,
    error: typeof s.error === 'string' ? s.error : undefined,
  }),
  component: LoginPage,
});

const callbackRoute = createRoute({ getParentRoute: () => rootRoute, path: '/auth/callback', component: AuthCallbackPage });
const catalogRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', beforeLoad: guard, component: CatalogPage });
const myApplicationsRoute = createRoute({
  getParentRoute: () => rootRoute, path: '/applications', beforeLoad: guard, validateSearch: validateListSearch,
  component: MyApplicationsPage,
});
const applicationRoute = createRoute({ getParentRoute: () => rootRoute, path: '/applications/$id', beforeLoad: guard, component: ApplicationPage });
const notificationsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/notifications', beforeLoad: guard, component: NotificationsPage });

export const router = createRouter({
  routeTree: rootRoute.addChildren([
    loginRoute, callbackRoute, catalogRoute, myApplicationsRoute, applicationRoute, notificationsRoute,
  ]),
});

declare module '@tanstack/react-router' {
  interface Register { router: typeof router }
}
