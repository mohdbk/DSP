import { useEffect, useState } from 'react';
import { Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import { Footer, Header, t, useLang, type Notification } from '@dsp/shared';
import { api, currentUser, signOut } from './session';
import { LoginPage, AuthCallbackPage } from './pages/Login';
import { CatalogPage } from './pages/Catalog';
import { MyApplicationsPage } from './pages/MyApplications';
import { ApplicationPage } from './pages/Application';
import { NotificationsPage } from './pages/Notifications';

export function App() {
  useLang(); // re-render everything when the language changes
  return (
    <Routes>
      <Route path="/login" element={<Shell bare><LoginPage /></Shell>} />
      <Route path="/auth/callback" element={<AuthCallbackPage />} />
      <Route path="/" element={<Protected><CatalogPage /></Protected>} />
      <Route path="/applications" element={<Protected><MyApplicationsPage /></Protected>} />
      <Route path="/applications/:id" element={<Protected><ApplicationPage /></Protected>} />
      <Route path="/notifications" element={<Protected><NotificationsPage /></Protected>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function Protected({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  if (!currentUser()) return <Navigate to={`/login?returnUrl=${encodeURIComponent(location.pathname)}`} replace />;
  return <Shell>{children}</Shell>;
}

function Shell({ children, bare }: { children: React.ReactNode; bare?: boolean }) {
  const user = currentUser();
  const [unread, setUnread] = useState(0);
  const location = useLocation();

  useEffect(() => {
    if (!user) return;
    api.get<Notification[]>('/api/notifications/me').then(n => setUnread(n.filter(x => !x.isRead).length)).catch(() => {});
  }, [location.pathname]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <>
      <Header
        title={t('app.portal')}
        subtitle={t('app.portalSub')}
        nav={!bare && user ? (
          <>
            <NavLink to="/" end>{t('nav.services')}</NavLink>
            <NavLink to="/applications">{t('nav.myApplications')}</NavLink>
          </>
        ) : undefined}
        right={!bare && user ? (
          <>
            <NavLink to="/notifications" className="bell" aria-label={t('notifications.unread', { n: unread })}>
              🔔{unread > 0 && <span className="count">{unread}</span>}
            </NavLink>
            <div className="user-chip">
              <div>{user.name}</div>
              <div className="role">{user.accountLevel ? `${t('uaePass')} · ${user.accountLevel}` : t('uaePass')}</div>
            </div>
            <button className="btn btn-ghost btn-sm" onClick={signOut}>{t('signOut')}</button>
          </>
        ) : undefined}
      />
      <main>{children}</main>
      <Footer />
    </>
  );
}
