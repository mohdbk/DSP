import { useEffect, useState } from 'react';
import { Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import { Footer, Header, type Notification } from '@dsp/shared';
import { api, currentUser, signOut } from './session';
import { LoginPage } from './pages/Login';
import { InboxPage } from './pages/Inbox';
import { TaskPage } from './pages/Task';
import { ApplicationsPage, ApplicationDetailPage } from './pages/Applications';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<Shell bare><LoginPage /></Shell>} />
      <Route path="/" element={<Navigate to="/inbox" replace />} />
      <Route path="/inbox" element={<Protected><InboxPage /></Protected>} />
      <Route path="/tasks/:id" element={<Protected><TaskPage /></Protected>} />
      <Route path="/applications" element={<Protected><ApplicationsPage /></Protected>} />
      <Route path="/applications/:id" element={<Protected><ApplicationDetailPage /></Protected>} />
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
        title="Back Office"
        subtitle="MoET E-Services · staff only"
        nav={!bare && user ? (
          <>
            <NavLink to="/inbox">Review inbox</NavLink>
            <NavLink to="/applications">All applications</NavLink>
          </>
        ) : undefined}
        right={!bare && user ? (
          <>
            <span className="bell" title={`${unread} unread notifications`}>🔔{unread > 0 && <span className="count">{unread}</span>}</span>
            <div className="user-chip">
              <div>{user.name}</div>
              <div className="role">{user.title ?? 'Staff'}</div>
            </div>
            <button className="btn btn-ghost btn-sm" onClick={signOut}>Sign out</button>
          </>
        ) : undefined}
      />
      <main>{children}</main>
      <Footer />
    </>
  );
}
