import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, formatMoney, loc, Spinner, t, type Application, type ServiceSummary } from '@dsp/shared';
import { api, currentUser } from '../session';

export function CatalogPage() {
  const navigate = useNavigate();
  const [services, setServices] = useState<ServiceSummary[]>();
  const [error, setError] = useState<string>();
  const [starting, setStarting] = useState<string>();

  useEffect(() => {
    api.get<ServiceSummary[]>('/api/eservices/services').then(setServices).catch(e => setError(e.message));
  }, []);

  async function start(code: string) {
    setStarting(code);
    try {
      const app = await api.post<Application>('/api/eservices/applications', { serviceCode: code });
      navigate(`/applications/${app.id}`);
    } catch (e) {
      setError((e as Error).message);
      setStarting(undefined);
    }
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h1>{t('catalog.welcome', { name: currentUser()?.name.split(' ')[0] ?? '' })}</h1>
          <p className="muted">{t('catalog.intro')}</p>
        </div>
      </div>
      {error && <Alert kind="error">{error}</Alert>}
      {!services && !error && <Spinner />}
      <div className="grid">
        {services?.map(s => (
          <article key={s.code} className="card service-card">
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <span className="tag">{loc(s.category, s.categoryAr)}</span>
              {s.workflowVersion === 2 && <span className="pill pill-ai">✨ {t('catalog.aiAssisted')}</span>}
            </div>
            <h2 style={{ margin: 0 }}>{loc(s.name, s.nameAr)}</h2>
            <p className="muted" style={{ margin: 0 }}>{loc(s.description, s.descriptionAr)}</p>
            <div className="meta">
              <span><strong>{formatMoney(s.fee, s.currency)}</strong> <span className="muted">· {t('steps', { n: s.stepCount })}</span></span>
              <button className="btn btn-primary btn-sm" disabled={!!starting} onClick={() => start(s.code)}>
                {starting === s.code ? t('catalog.starting') : t('catalog.apply')}
              </button>
            </div>
          </article>
        ))}
      </div>
    </>
  );
}
