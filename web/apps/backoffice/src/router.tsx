import { createRootRoute, createRoute, createRouter, redirect } from '@tanstack/react-router';
import { countUnread, t } from '@dsp/core';
import { EmptyState, PortalFrame, requireSignedIn, validateListSearch } from '@dsp/ui';
import { api, currentUser, signOut } from './session';
import { LoginPage } from './pages/Login';
import { InboxPage } from './pages/Inbox';
import { TaskPage } from './pages/Task';
import { ApplicationDetailPage, ApplicationsPage } from './pages/Applications';
import { ServicesPage } from './pages/builder/Services';
import { NewServicePage } from './pages/builder/NewService';
import { ServiceEditorPage } from './pages/builder/ServiceEditor';

/** The back-office frame: government header and footer, staff navigation and the signed-in officer. */
function Root() {
  const user = currentUser();
  return (
    <PortalFrame service={t('app.backoffice')} publicPaths={['/login']} onSignOut={signOut}
      user={user && { name: user.name, role: user.title ?? t('staff') }}
      loadUnread={() => countUnread(api)}
      navItems={pathname => [
        { href: '/inbox', label: t('nav.inbox'), active: pathname.startsWith('/inbox') || pathname.startsWith('/tasks') },
        { href: '/applications', label: t('nav.allApplications'), active: pathname.startsWith('/applications') },
        { href: '/services', label: t('nav.builder'), active: pathname.startsWith('/services') },
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
const newServiceRoute = createRoute({ getParentRoute: () => rootRoute, path: '/services/new', beforeLoad: guard, component: NewServicePage });
const serviceRoute = createRoute({ getParentRoute: () => rootRoute, path: '/services/$code', beforeLoad: guard, component: ServiceEditorPage });

export const router = createRouter({
  routeTree: rootRoute.addChildren([
    indexRoute, loginRoute, inboxRoute, taskRoute, applicationsRoute, applicationRoute, servicesRoute, newServiceRoute, serviceRoute,
  ]),
});

declare module '@tanstack/react-router' {
  interface Register { router: typeof router }
}
