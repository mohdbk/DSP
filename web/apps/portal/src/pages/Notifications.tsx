import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, EmptyState, formatDate, loc, Spinner, t, type Notification } from '@dsp/shared';
import { api } from '../session';

export function NotificationsPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<Notification[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    api.get<Notification[]>('/api/notifications/me').then(setItems).catch(e => setError(e.message));
  }, []);

  async function open(n: Notification) {
    if (!n.isRead) await api.post(`/api/notifications/${n.id}/read`).catch(() => {});
    if (n.link) navigate(n.link);
    else setItems(items?.map(i => (i.id === n.id ? { ...i, isRead: true } : i)));
  }

  return (
    <>
      <div className="page-head"><h1>{t('notifications.title')}</h1></div>
      {error && <Alert kind="error">{error}</Alert>}
      {!items && !error && <Spinner />}
      {items?.length === 0 && <EmptyState title={t('notifications.empty')} />}
      <div className="notice-list">
        {items?.map(n => (
          <button key={n.id} className={`card notice ${n.isRead ? '' : 'unread'}`}
            style={{ textAlign: 'start', cursor: 'pointer', font: 'inherit' }} onClick={() => open(n)}>
            <div style={{ flex: 1 }}>
              <strong>{loc(n.title, n.titleAr)}</strong>
              <div>{loc(n.body, n.bodyAr)}</div>
            </div>
            <div className="muted" style={{ fontSize: 13, whiteSpace: 'nowrap' }}>{formatDate(n.createdAt, true)}</div>
          </button>
        ))}
      </div>
    </>
  );
}
