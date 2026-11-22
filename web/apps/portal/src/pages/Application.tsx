import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Alert, ApiError, formatMoney, Spinner, StatusBadge,
  type Application, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';
import { FormView } from './FormView';
import { ChatView } from './ChatView';
import { ApplicationStatusView } from './ApplicationStatusView';

type ViewMode = 'form' | 'chat';

export function ApplicationPage() {
  const { id } = useParams();
  const [app, setApp] = useState<Application>();
  const [service, setService] = useState<ServiceDefinition>();
  const [error, setError] = useState<string>();
  const [mode, setMode] = useState<ViewMode>(() => (localStorage.getItem('dsp.view') as ViewMode) ?? 'form');

  const load = useCallback(async () => {
    try {
      const a = await api.get<Application>(`/api/eservices/applications/${id}`);
      setApp(a);
      if (!service) setService(await api.get<ServiceDefinition>(`/api/eservices/services/${a.serviceCode}`));
    } catch (e) {
      setError(e instanceof ApiError && e.status === 404 ? 'Application not found.' : (e as Error).message);
    }
  }, [id, service]);

  useEffect(() => { load(); }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  function switchMode(m: ViewMode) {
    setMode(m);
    localStorage.setItem('dsp.view', m);
    load(); // both views share the same saved data; refresh when switching
  }

  if (error) return <Alert kind="error">{error}</Alert>;
  if (!app || !service) return <Spinner />;

  const isDraft = app.status === 'Draft';
  const required = service.steps.flatMap(s => s.fields).filter(f => f.required);
  const filled = required.filter(f => !!app.data[f.key]).length;

  return (
    <>
      <div className="page-head">
        <div>
          <div className="muted">{app.number}</div>
          <h1>{service.name}</h1>
          <StatusBadge status={app.status} />
        </div>
        {isDraft && (
          <div className="toggle" role="tablist" aria-label="How do you want to apply?">
            <button role="tab" aria-selected={mode === 'form'} className={mode === 'form' ? 'on' : ''} onClick={() => switchMode('form')}>📝 Form</button>
            <button role="tab" aria-selected={mode === 'chat'} className={mode === 'chat' ? 'on' : ''} onClick={() => switchMode('chat')}>💬 Chat</button>
          </div>
        )}
      </div>

      {isDraft ? (
        <div className="split">
          <div>
            {mode === 'form'
              ? <FormView app={app} service={service} onChange={setApp} />
              : <ChatView app={app} onApplication={partial => {
                  if (partial.status !== 'Draft') load();
                  else setApp({ ...app, ...partial });
                }} />}
          </div>
          <aside className="card">
            <h3>Your application</h3>
            <dl className="kv">
              <dt>Number</dt><dd>{app.number}</dd>
              <dt>Fee</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
              <dt>Progress</dt><dd>{filled} of {required.length} required details</dd>
            </dl>
            <p className="muted" style={{ fontSize: 13, marginTop: 16 }}>
              The form and the chat save to the same application. Start in one and finish in the other.
              The fee is payable only after the ministry approves your application.
            </p>
          </aside>
        </div>
      ) : (
        <ApplicationStatusView app={app} service={service} onRefresh={load} />
      )}
    </>
  );
}
