import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Alert, EmptyState, formatDate, Spinner, StatusBadge, type Application } from '@dsp/shared';
import { api } from '../session';

export function MyApplicationsPage() {
  const navigate = useNavigate();
  const [apps, setApps] = useState<Application[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    api.get<Application[]>('/api/eservices/applications').then(setApps).catch(e => setError(e.message));
  }, []);

  return (
    <>
      <div className="page-head"><h1>My applications</h1></div>
      {error && <Alert kind="error">{error}</Alert>}
      {!apps && !error && <Spinner />}
      {apps?.length === 0 && (
        <EmptyState title="No applications yet"><Link to="/">Browse services</Link> to start one.</EmptyState>
      )}
      {apps && apps.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>Number</th><th>Service</th><th>Reference</th><th>Status</th><th>Last update</th></tr>
            </thead>
            <tbody>
              {apps.map(a => (
                <tr key={a.id} className="clickable" onClick={() => navigate(`/applications/${a.id}`)}>
                  <td><strong>{a.number}</strong></td>
                  <td>{a.serviceName}</td>
                  <td>{a.referenceValue ?? <span className="muted">—</span>}</td>
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
