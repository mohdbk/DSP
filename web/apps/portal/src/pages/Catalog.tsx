import { useEffect, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { t, type Application, type ServiceSummary } from '@dsp/core';
import { CardGrid, Loading, Notice, PageHeader, ServiceCard } from '@dsp/ui';
import { api, currentUser } from '../session';

export function CatalogPage() {
  const navigate = useNavigate();
  const [services, setServices] = useState<ServiceSummary[]>();
  const [error, setError] = useState<string>();
  const [starting, setStarting] = useState<string>();

  useEffect(() => {
    api.list<ServiceSummary>('/api/v1/eservices/services').then(setServices).catch(e => setError(e.message));
  }, []);

  async function start(code: string) {
    setStarting(code);
    try {
      const app = await api.post<Application>('/api/v1/eservices/applications', { serviceCode: code });
      navigate({ to: '/applications/$id', params: { id: app.id } });
    } catch (e) {
      setError((e as Error).message);
      setStarting(undefined);
    }
  }

  return (
    <>
      <PageHeader title={t('catalog.welcome', { name: currentUser()?.name.split(' ')[0] ?? '' })} intro={t('catalog.intro')} />
      {error && <Notice kind="error">{error}</Notice>}
      {!services && !error && <Loading />}
      <CardGrid>
        {services?.map(s => (
          <ServiceCard key={s.code} service={s} busy={starting === s.code} disabled={!!starting && starting !== s.code}
            onApply={() => start(s.code)} />
        ))}
      </CardGrid>
    </>
  );
}
