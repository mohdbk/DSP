import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, formatMoney, Spinner, type Application, type ServiceSummary } from '@dsp/shared';
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
          <h1>Welcome, {currentUser()?.name.split(' ')[0]}</h1>
          <p className="muted">Choose a service to start. You can fill in a form or chat with the assistant — switch any time.</p>
        </div>
      </div>
      {error && <Alert kind="error">{error}</Alert>}
      {!services && !error && <Spinner />}
      <div className="grid">
        {services?.map(s => (
          <article key={s.code} className="card service-card">
            <span className="tag">{s.category}</span>
            <h2 style={{ margin: 0 }}>{s.name}</h2>
            {s.nameAr && <div className="muted" dir="rtl" lang="ar">{s.nameAr}</div>}
            <p className="muted" style={{ margin: 0 }}>{s.description}</p>
            <div className="meta">
              <span><strong>{formatMoney(s.fee, s.currency)}</strong> <span className="muted">· {s.stepCount} steps</span></span>
              <button className="btn btn-primary btn-sm" disabled={!!starting} onClick={() => start(s.code)}>
                {starting === s.code ? 'Starting…' : 'Apply'}
              </button>
            </div>
          </article>
        ))}
      </div>
    </>
  );
}
