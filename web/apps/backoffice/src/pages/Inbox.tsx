import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, EmptyState, formatDate, Spinner, type ReviewTask } from '@dsp/shared';
import { api } from '../session';

export function InboxPage() {
  const navigate = useNavigate();
  const [tasks, setTasks] = useState<ReviewTask[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    const load = () => api.get<ReviewTask[]>('/api/eservices/tasks').then(setTasks).catch(e => setError(e.message));
    load();
    const t = setInterval(load, 15000);
    return () => clearInterval(t);
  }, []);

  const now = Date.now();

  return (
    <>
      <div className="page-head">
        <div>
          <h1>Review inbox</h1>
          <p className="muted">Applications waiting for a decision, oldest first.</p>
        </div>
        {tasks && <span className="tag">{tasks.length} open</span>}
      </div>
      {error && <Alert kind="error">{error}</Alert>}
      {!tasks && !error && <Spinner />}
      {tasks?.length === 0 && <EmptyState title="Inbox is clear">New applications appear here as citizens submit them.</EmptyState>}
      {tasks && tasks.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>Application</th><th>Service</th><th>Applicant</th><th>Reference</th><th>Received</th><th>SLA</th></tr>
            </thead>
            <tbody>
              {tasks.map(t => {
                const overdue = new Date(t.dueAt).getTime() < now;
                return (
                  <tr key={t.id} className="clickable" onClick={() => navigate(`/tasks/${t.id}`)}>
                    <td><strong>{t.applicationNumber}</strong></td>
                    <td>{t.serviceName}</td>
                    <td>{t.applicantName}</td>
                    <td>{t.referenceValue ?? '—'}</td>
                    <td>{formatDate(t.createdAt, true)}</td>
                    <td>{overdue
                      ? <span className="badge badge-Rejected">Overdue</span>
                      : <span className="muted">Due {formatDate(t.dueAt)}</span>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
