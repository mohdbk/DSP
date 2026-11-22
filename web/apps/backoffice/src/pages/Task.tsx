import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AiReviewPanel, Alert, DynamicForm, formatDate, formatMoney, loc, openBlob, Spinner, StatusBadge, t,
  type AiAssessment, type Application, type ReviewTask, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';

/** Review one application: the same schema renders the citizen's answers read-only, next to the AI pre-review. */
export function TaskPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [task, setTask] = useState<ReviewTask>();
  const [app, setApp] = useState<Application>();
  const [service, setService] = useState<ServiceDefinition>();
  const [assessments, setAssessments] = useState<AiAssessment[]>();
  const [comment, setComment] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [done, setDone] = useState<Application>();

  useEffect(() => {
    (async () => {
      try {
        const task = await api.get<ReviewTask>(`/api/eservices/tasks/${id}`);
        setTask(task);
        const a = await api.get<Application>(`/api/eservices/applications/${task.applicationId}`);
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

  async function decide(approve: boolean) {
    if (!approve && !comment.trim()) {
      setError(t('task.reasonRequired'));
      return;
    }
    setBusy(true);
    setError(undefined);
    try {
      setDone(await api.post<Application>(`/api/eservices/tasks/${id}/decision`, { approve, comment: comment.trim() || null }));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  if (error && !task) return <Alert kind="error">{error}</Alert>;
  if (!task || !app || !service) return <Spinner />;

  return (
    <>
      <div className="page-head">
        <div>
          <div className="muted"><bdi>{app.number}</bdi></div>
          <h1>{loc(service.name, service.nameAr)}</h1>
          <StatusBadge status={(done ?? app).status} />
        </div>
        <button className="btn btn-ghost" onClick={() => navigate('/inbox')}>{t('task.backToInbox')}</button>
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
            <h3>{t('task.application')}</h3>
            <dl className="kv">
              <dt>{t('applicant')}</dt><dd>{app.applicantName}</dd>
              <dt>{t('submitted')}</dt><dd>{formatDate(app.submittedAt, true)}</dd>
              <dt>{t('slaDue')}</dt><dd>{formatDate(task.dueAt, true)}</dd>
              <dt>{t('fee')}</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
              <dt>{t('workflow')}</dt><dd>{t(app.workflowVersion === 2 ? 'process.v2' : 'process.v1')} · v{app.serviceVersion}</dd>
            </dl>
          </div>

          <AiReviewPanel assessments={assessments} workflowVersion={app.workflowVersion} />

          <div className="card">
            <h3>{t('decision')}</h3>
            {done ? (
              <Alert kind="success">{t('task.recorded')}</Alert>
            ) : task.status === 'Completed' ? (
              <Alert kind="info">{t('task.alreadyDone', { outcome: task.outcome ?? '' })}</Alert>
            ) : (
              <>
                {error && <Alert kind="error">{error}</Alert>}
                <div className="field">
                  <label htmlFor="comment">{t('task.commentLabel')} <span className="muted">{t('task.commentHint')}</span></label>
                  <textarea id="comment" rows={4} value={comment} onChange={e => setComment(e.target.value)}
                    placeholder={t('task.commentPlaceholder')} dir="auto" />
                </div>
                <div style={{ display: 'flex', gap: 10 }}>
                  <button className="btn btn-primary" style={{ flex: 1 }} disabled={busy} onClick={() => decide(true)}>{t('task.approve')}</button>
                  <button className="btn btn-danger" style={{ flex: 1 }} disabled={busy} onClick={() => decide(false)}>{t('task.reject')}</button>
                </div>
              </>
            )}
          </div>
        </aside>
      </div>
    </>
  );
}
