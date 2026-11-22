import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Alert, ApiError, formatMoney, getLang, loc, Spinner, StatusBadge, t,
  type Application, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';
import { FormView } from './FormView';
import { ChatView } from './ChatView';
import { ApplicationStatusView } from './ApplicationStatusView';

type ViewMode = 'form' | 'chat';

function savedMode(): ViewMode {
  try { return localStorage.getItem('dsp.view') === 'chat' ? 'chat' : 'form'; } catch { return 'form'; }
}

export function ApplicationPage() {
  const { id } = useParams();
  const [app, setApp] = useState<Application>();
  const [service, setService] = useState<ServiceDefinition>();
  const [error, setError] = useState<string>();
  const [mode, setMode] = useState<ViewMode>(savedMode);

  const load = useCallback(async () => {
    try {
      const a = await api.get<Application>(`/api/eservices/applications/${id}`);
      setApp(a);
      // The version the application was started on, so the form matches what was filled in.
      if (!service) setService(await api.get<ServiceDefinition>(`/api/eservices/services/${a.serviceCode}?version=${a.serviceVersion}`));
    } catch (e) {
      setError(e instanceof ApiError && e.status === 404 ? t('app.notFound') : (e as Error).message);
    }
  }, [id, service]);

  useEffect(() => { load(); }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  function switchMode(m: ViewMode) {
    setMode(m);
    try { localStorage.setItem('dsp.view', m); } catch { /* not saved */ }
    load(); // both views share the same saved data; refresh when switching
  }

  if (error) return <Alert kind="error">{error}</Alert>;
  if (!app || !service) return <Spinner />;

  const isDraft = app.status === 'Draft';
  const infoRequested = app.status === 'InformationRequested';
  const required = service.steps.flatMap(s => s.fields).filter(f => f.required);
  const filled = required.filter(f => !!app.data[f.key]).length;
  const ar = getLang() === 'ar';

  return (
    <>
      <div className="page-head">
        <div>
          <div className="muted"><bdi>{app.number}</bdi></div>
          <h1>{loc(service.name, service.nameAr)}</h1>
          <StatusBadge status={app.status} />
        </div>
        {isDraft && (
          <div className="toggle" role="tablist" aria-label={t('app.howToApply')}>
            <button role="tab" aria-selected={mode === 'form'} className={mode === 'form' ? 'on' : ''} onClick={() => switchMode('form')}>{t('app.form')}</button>
            <button role="tab" aria-selected={mode === 'chat'} className={mode === 'chat' ? 'on' : ''} onClick={() => switchMode('chat')}>{t('app.chat')}</button>
          </div>
        )}
      </div>

      {infoRequested && (
        <div className="info-request" role="status">
          <h2>{t('app.infoNeeded')}</h2>
          <p style={{ margin: 0 }}>{t('app.infoNeededIntro')}</p>
          <ul>{app.informationRequest.map((q, i) => <li key={i}>{ar ? q.ar : q.en}</li>)}</ul>
          <p className="muted" style={{ margin: 0 }}>{t('app.infoNeededHow')}</p>
        </div>
      )}

      {isDraft || infoRequested ? (
        <div className="split">
          <div>
            {mode === 'form' || infoRequested
              ? <FormView app={app} service={service} onChange={setApp} resubmit={infoRequested} />
              : <ChatView app={app} onApplication={partial => {
                  if (partial.status !== 'Draft') load();
                  else setApp({ ...app, ...partial });
                }} />}
          </div>
          <aside className="card">
            <h3>{t('app.yours')}</h3>
            <dl className="kv">
              <dt>{t('number')}</dt><dd><bdi>{app.number}</bdi></dd>
              <dt>{t('fee')}</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
              <dt>{t('app.progress')}</dt><dd>{t('app.progressValue', { filled, total: required.length })}</dd>
            </dl>
            <p className="muted" style={{ fontSize: 13, marginTop: 16 }}>{t('app.sideNote')}</p>
          </aside>
        </div>
      ) : (
        <ApplicationStatusView app={app} service={service} onRefresh={load} />
      )}
    </>
  );
}
