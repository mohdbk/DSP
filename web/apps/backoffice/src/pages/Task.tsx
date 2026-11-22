import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert, DynamicForm, formatDate, formatMoney, openBlob, Spinner, StatusBadge,
  type Application, type ReviewTask, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';

/** Review one application: the same schema renders the citizen's answers read-only. */
export function TaskPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [task, setTask] = useState<ReviewTask>();
  const [app, setApp] = useState<Application>();
  const [service, setService] = useState<ServiceDefinition>();
  const [comment, setComment] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [done, setDone] = useState<Application>();

  useEffect(() => {
    (async () => {
      try {
        const t = await api.get<ReviewTask>(`/api/eservices/tasks/${id}`);
        setTask(t);
        const a = await api.get<Application>(`/api/eservices/applications/${t.applicationId}`);
        setApp(a);
        setService(await api.get<ServiceDefinition>(`/api/eservices/services/${a.serviceCode}`));
      } catch (e) {
        setError((e as Error).message);
      }
    })();
  }, [id]);

  async function decide(approve: boolean) {
    if (!approve && !comment.trim()) {
      setError('Add a reason before rejecting. The applicant will see it.');
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
          <div className="muted">{app.number}</div>
          <h1>{service.name}</h1>
          <StatusBadge status={(done ?? app).status} />
        </div>
        <button className="btn btn-ghost" onClick={() => navigate('/inbox')}>← Back to inbox</button>
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

        <aside style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className="card">
            <h3>Application</h3>
            <dl className="kv">
              <dt>Applicant</dt><dd>{app.applicantName}</dd>
              <dt>Submitted</dt><dd>{formatDate(app.submittedAt, true)}</dd>
              <dt>SLA due</dt><dd>{formatDate(task.dueAt, true)}</dd>
              <dt>Fee</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
              <dt>Workflow</dt><dd>v{app.workflowVersion}</dd>
            </dl>
          </div>

          <div className="card">
            <h3>Decision</h3>
            {done ? (
              <Alert kind="success">
                Decision recorded. The application is now <strong>{done.status === 'AwaitingPayment' ? 'awaiting payment' : done.status.toLowerCase()}</strong> and the applicant has been notified.
              </Alert>
            ) : task.status === 'Completed' ? (
              <Alert kind="info">This task was completed ({task.outcome}).</Alert>
            ) : (
              <>
                {error && <Alert kind="error">{error}</Alert>}
                <div className="field">
                  <label htmlFor="comment">Comment <span className="muted">(required to reject)</span></label>
                  <textarea id="comment" rows={4} value={comment} onChange={e => setComment(e.target.value)}
                    placeholder="Visible to the applicant" />
                </div>
                <div style={{ display: 'flex', gap: 10 }}>
                  <button className="btn btn-primary" style={{ flex: 1 }} disabled={busy} onClick={() => decide(true)}>Approve</button>
                  <button className="btn btn-danger" style={{ flex: 1 }} disabled={busy} onClick={() => decide(false)}>Reject</button>
                </div>
              </>
            )}
          </div>
        </aside>
      </div>
    </>
  );
}
