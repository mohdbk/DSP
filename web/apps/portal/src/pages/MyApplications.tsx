import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Alert, EmptyState, formatDate, Spinner, StatusBadge, t, type Application } from '@dsp/shared';
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
      <div className="page-head"><h1>{t('my.title')}</h1></div>
      {error && <Alert kind="error">{error}</Alert>}
      {!apps && !error && <Spinner />}
      {apps?.length === 0 && (
        <EmptyState title={t('my.empty')}><Link to="/">{t('my.browse')}</Link> {t('my.toStart')}</EmptyState>
      )}
      {apps && apps.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>{t('number')}</th><th>{t('service')}</th><th>{t('reference')}</th><th>{t('status')}</th><th>{t('lastUpdate')}</th></tr>
            </thead>
            <tbody>
              {apps.map(a => (
                <tr key={a.id} className="clickable" onClick={() => navigate(`/applications/${a.id}`)}>
                  <td><strong><bdi>{a.number}</bdi></strong></td>
                  <td>{a.serviceName}</td>
                  <td>{a.referenceValue ? <bdi>{a.referenceValue}</bdi> : <span className="muted">—</span>}</td>
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
