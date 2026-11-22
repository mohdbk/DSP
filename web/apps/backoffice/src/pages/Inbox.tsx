import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, EmptyState, formatDate, RecommendationChip, Spinner, t, type ReviewTask } from '@dsp/shared';
import { api } from '../session';

export function InboxPage() {
  const navigate = useNavigate();
  const [tasks, setTasks] = useState<ReviewTask[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    const load = () => api.get<ReviewTask[]>('/api/eservices/tasks').then(setTasks).catch(e => setError(e.message));
    load();
    const timer = setInterval(load, 15000);
    return () => clearInterval(timer);
  }, []);

  const now = Date.now();

  return (
    <>
      <div className="page-head">
        <div>
          <h1>{t('inbox.title')}</h1>
          <p className="muted">{t('inbox.intro')}</p>
        </div>
        {tasks && <span className="tag">{t('inbox.open', { n: tasks.length })}</span>}
      </div>
      {error && <Alert kind="error">{error}</Alert>}
      {!tasks && !error && <Spinner />}
      {tasks?.length === 0 && <EmptyState title={t('inbox.empty')}>{t('inbox.emptyHint')}</EmptyState>}
      {tasks && tasks.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>{t('inbox.application')}</th><th>{t('service')}</th><th>{t('applicant')}</th><th>{t('reference')}</th>
                <th>{t('inbox.ai')}</th><th>{t('received')}</th><th>{t('sla')}</th>
              </tr>
            </thead>
            <tbody>
              {tasks.map(task => {
                const overdue = new Date(task.dueAt).getTime() < now;
                return (
                  <tr key={task.id} className="clickable" onClick={() => navigate(`/tasks/${task.id}`)}>
                    <td><strong><bdi>{task.applicationNumber}</bdi></strong></td>
                    <td>{task.serviceName}</td>
                    <td>{task.applicantName}</td>
                    <td>{task.referenceValue ? <bdi>{task.referenceValue}</bdi> : '—'}</td>
                    <td><RecommendationChip value={task.aiRecommendation} short /></td>
                    <td>{formatDate(task.createdAt, true)}</td>
                    <td>{overdue
                      ? <span className="badge badge-Rejected">{t('overdue')}</span>
                      : <span className="muted">{t('due', { date: formatDate(task.dueAt) })}</span>}</td>
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
