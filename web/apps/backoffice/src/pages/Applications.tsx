import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert, DynamicForm, EmptyState, formatDate, formatMoney, openBlob, Spinner, StatusBadge,
  type Application, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';

export function ApplicationsPage() {
  const navigate = useNavigate();
  const [apps, setApps] = useState<Application[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    api.get<Application[]>('/api/eservices/applications?all=true').then(setApps).catch(e => setError(e.message));
  }, []);

  return (
    <>
      <div className="page-head"><h1>All applications</h1></div>
      {error && <Alert kind="error">{error}</Alert>}
      {!apps && !error && <Spinner />}
      {apps?.length === 0 && <EmptyState title="No submitted applications yet" />}
      {apps && apps.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>Number</th><th>Service</th><th>Applicant</th><th>Reference</th><th>Status</th><th>Updated</th></tr>
            </thead>
            <tbody>
              {apps.map(a => (
                <tr key={a.id} className="clickable" onClick={() => navigate(`/applications/${a.id}`)}>
                  <td><strong>{a.number}</strong></td>
                  <td>{a.serviceName}</td>
                  <td>{a.applicantName}</td>
                  <td>{a.referenceValue ?? '—'}</td>
                  <td><StatusBadge status={a.status} /></td>
                  <td>{formatDate(a.updatedAt, true)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

export function ApplicationDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [app, setApp] = useState<Application>();
  const [service, setService] = useState<ServiceDefinition>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    (async () => {
      try {
        const a = await api.get<Application>(`/api/eservices/applications/${id}`);
        setApp(a);
        setService(await api.get<ServiceDefinition>(`/api/eservices/services/${a.serviceCode}`));
      } catch (e) {
        setError((e as Error).message);
      }
    })();
  }, [id]);

  if (error) return <Alert kind="error">{error}</Alert>;
  if (!app || !service) return <Spinner />;

  return (
    <>
      <div className="page-head">
        <div>
          <div className="muted">{app.number}</div>
          <h1>{service.name}</h1>
          <StatusBadge status={app.status} />
        </div>
        <button className="btn btn-ghost" onClick={() => navigate('/applications')}>← All applications</button>
      </div>
      <div className="split">
        <div className="card">
          {service.steps.map(s => (
            <section key={s.id} style={{ marginBottom: 16 }}>
              <h3>{s.title}</h3>
              <DynamicForm step={{ ...s, description: null }} values={app.data} readOnly
                onOpenFile={async docId => openBlob(await api.download(`/api/documents/documents/${docId}/content`))} />
            </section>
          ))}
        </div>
        <aside className="card">
          <dl className="kv">
            <dt>Applicant</dt><dd>{app.applicantName}</dd>
            <dt>Submitted</dt><dd>{formatDate(app.submittedAt, true)}</dd>
            <dt>Decision</dt><dd>{app.approved == null ? '—' : app.approved ? 'Approved' : 'Rejected'}</dd>
            {app.decisionComment && <><dt>Comment</dt><dd>{app.decisionComment}</dd></>}
            <dt>Fee</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
            <dt>Completed</dt><dd>{formatDate(app.completedAt, true) || '—'}</dd>
          </dl>
          {app.certificateDocumentId && (
            <button className="btn btn-secondary" style={{ marginTop: 16, width: '100%' }}
              onClick={async () => openBlob(await api.download(`/api/documents/documents/${app.certificateDocumentId}/content`))}>
              View certificate
            </button>
          )}
        </aside>
      </div>
    </>
  );
}
