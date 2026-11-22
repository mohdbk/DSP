import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AiReviewPanel, Alert, DynamicForm, EmptyState, formatDate, formatMoney, loc, openBlob, Spinner, StatusBadge, t,
  type AiAssessment, type Application, type ServiceDefinition,
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
      <div className="page-head"><h1>{t('apps.title')}</h1></div>
      {error && <Alert kind="error">{error}</Alert>}
      {!apps && !error && <Spinner />}
      {apps?.length === 0 && <EmptyState title={t('apps.empty')} />}
      {apps && apps.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>{t('number')}</th><th>{t('service')}</th><th>{t('applicant')}</th><th>{t('reference')}</th><th>{t('status')}</th><th>{t('updated')}</th></tr>
            </thead>
            <tbody>
              {apps.map(a => (
                <tr key={a.id} className="clickable" onClick={() => navigate(`/applications/${a.id}`)}>
                  <td><strong><bdi>{a.number}</bdi></strong></td>
                  <td>{a.serviceName} {a.workflowVersion === 2 && <span className="ai-tag" title={t('process.v2')}>✨</span>}</td>
                  <td>{a.applicantName}</td>
                  <td>{a.referenceValue ? <bdi>{a.referenceValue}</bdi> : '—'}</td>
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
  const [assessments, setAssessments] = useState<AiAssessment[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    (async () => {
      try {
        const a = await api.get<Application>(`/api/eservices/applications/${id}`);
        setApp(a);
        const [svc, ai] = await Promise.all([
          api.get<ServiceDefinition>(`/api/eservices/services/${a.serviceCode}?version=${a.serviceVersion}`),
          api.get<AiAssessment[]>(`/api/eservices/applications/${a.id}/assessments`),
        ]);
        setService(svc);
        setAssessments(ai);
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
          <div className="muted"><bdi>{app.number}</bdi></div>
          <h1>{loc(service.name, service.nameAr)}</h1>
          <StatusBadge status={app.status} />
        </div>
        <button className="btn btn-ghost" onClick={() => navigate('/applications')}>{t('apps.back')}</button>
      </div>
      <div className="split">
        <div className="card">
          {service.steps.map(s => (
            <section key={s.id} style={{ marginBottom: 16 }}>
              <h3>{loc(s.title, s.titleAr)}</h3>
              <DynamicForm step={{ ...s, description: null }} values={app.data} readOnly
                onOpenFile={async docId => openBlob(await api.download(`/api/documents/documents/${docId}/content`))} />
            </section>
          ))}
        </div>
        <aside style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className="card">
            <dl className="kv">
              <dt>{t('applicant')}</dt><dd>{app.applicantName}</dd>
              <dt>{t('submitted')}</dt><dd>{formatDate(app.submittedAt, true)}</dd>
              <dt>{t('workflow')}</dt><dd>{t(app.workflowVersion === 2 ? 'process.v2' : 'process.v1')} · v{app.serviceVersion}</dd>
              <dt>{t('decision')}</dt><dd>{app.approved == null ? '—' : app.approved ? t('approved') : t('rejected')}</dd>
              {app.decisionComment && <><dt>{t('comment')}</dt><dd>{app.decisionComment}</dd></>}
              <dt>{t('fee')}</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
              <dt>{t('completed')}</dt><dd>{formatDate(app.completedAt, true) || '—'}</dd>
            </dl>
            {app.certificateDocumentId && (
              <button className="btn btn-secondary" style={{ marginTop: 16, width: '100%' }}
                onClick={async () => openBlob(await api.download(`/api/documents/documents/${app.certificateDocumentId}/content`))}>
                {t('apps.viewCertificate')}
              </button>
            )}
          </div>
          <AiReviewPanel assessments={assessments} workflowVersion={app.workflowVersion} />
        </aside>
      </div>
    </>
  );
}
