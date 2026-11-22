import { useEffect, useState } from 'react';
import { Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import { Footer, Header, t, useLang, type Notification } from '@dsp/shared';
import { api, currentUser, signOut } from './session';
import { LoginPage } from './pages/Login';
import { InboxPage } from './pages/Inbox';
import { TaskPage } from './pages/Task';
import { ApplicationsPage, ApplicationDetailPage } from './pages/Applications';
import { ServicesPage } from './pages/builder/Services';
import { ServiceEditorPage, NewServicePage } from './pages/builder/ServiceEditor';

export function App() {
  useLang(); // re-render everything when the language changes
  return (
    <Routes>
      <Route path="/login" element={<Shell bare><LoginPage /></Shell>} />
      <Route path="/" element={<Navigate to="/inbox" replace />} />
      <Route path="/inbox" element={<Protected><InboxPage /></Protected>} />
      <Route path="/tasks/:id" element={<Protected><TaskPage /></Protected>} />
      <Route path="/applications" element={<Protected><ApplicationsPage /></Protected>} />
      <Route path="/applications/:id" element={<Protected><ApplicationDetailPage /></Protected>} />
      <Route path="/services" element={<Protected><ServicesPage /></Protected>} />
      <Route path="/services/new" element={<Protected><NewServicePage /></Protected>} />
      <Route path="/services/:code" element={<Protected><ServiceEditorPage /></Protected>} />
      <Route path="*" element={<Navigate to="/inbox" replace />} />
    </Routes>
  );
}

function Protected({ children }: { children: React.ReactNode }) {
  if (!currentUser()) return <Navigate to="/login" replace />;
  return <Shell>{children}</Shell>;
}

function Shell({ children, bare }: { children: React.ReactNode; bare?: boolean }) {
  const user = currentUser();
  const location = useLocation();
  const [unread, setUnread] = useState(0);

  useEffect(() => {
    if (!user) return;
    api.get<Notification[]>('/api/notifications/me').then(n => setUnread(n.filter(x => !x.isRead).length)).catch(() => {});
  }, [location.pathname]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <>
      <Header
        title={t('app.backoffice')}
        subtitle={t('app.backofficeSub')}
        nav={!bare && user ? (
          <>
            <NavLink to="/inbox">{t('nav.inbox')}</NavLink>
            <NavLink to="/applications">{t('nav.allApplications')}</NavLink>
            <NavLink to="/services">{t('nav.builder')}</NavLink>
          </>
        ) : undefined}
        right={!bare && user ? (
          <>
            <span className="bell" title={t('notifications.unread', { n: unread })}>🔔{unread > 0 && <span className="count">{unread}</span>}</span>
            <div className="user-chip">
              <div>{user.name}</div>
              <div className="role">{user.title ?? t('staff')}</div>
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
