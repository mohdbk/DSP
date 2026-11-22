import { useEffect, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { t, type Notification } from '@dsp/core';
import { EmptyState, Loading, Notice, NotificationList, PageHeader } from '@dsp/ui';
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
    if (n.link) navigate({ href: n.link });
    else setItems(items?.map(i => (i.id === n.id ? { ...i, isRead: true } : i)));
  }

  return (
    <>
      <PageHeader title={t('notifications.title')} />
      {error && <Notice kind="error">{error}</Notice>}
      {!items && !error && <Loading />}
      {items?.length === 0 && <EmptyState title={t('notifications.empty')} />}
      {items && items.length > 0 && <NotificationList items={items} onOpen={open} />}
    </>
  );
}
