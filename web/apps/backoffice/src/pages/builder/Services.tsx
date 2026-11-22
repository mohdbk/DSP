import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Alert, EmptyState, formatDate, loc, Spinner, t, type BuilderServiceSummary } from '@dsp/shared';
import { api } from '../../session';

/** Every e-service, its live and draft versions. */
export function ServicesPage() {
  const navigate = useNavigate();
  const [services, setServices] = useState<BuilderServiceSummary[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    api.get<BuilderServiceSummary[]>('/api/eservices/builder/services').then(setServices).catch(e => setError(e.message));
  }, []);

  return (
    <>
      <div className="page-head">
        <div>
          <h1>{t('builder.title')}</h1>
          <p className="muted" style={{ maxWidth: 720 }}>{t('builder.intro')}</p>
        </div>
        <Link className="btn btn-primary" to="/services/new">+ {t('builder.new')}</Link>
      </div>
      {error && <Alert kind="error">{error}</Alert>}
      {!services && !error && <Spinner />}
      {services?.length === 0 && <EmptyState title={t('builder.empty')} />}
      {services && services.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>{t('service')}</th><th>{t('status')}</th><th>{t('builder.process')}</th><th>{t('builder.lastChange')}</th></tr>
            </thead>
            <tbody>
              {services.map(s => (
                <tr key={s.code} className="clickable" onClick={() => navigate(`/services/${s.code}`)}>
                  <td>
                    <strong>{loc(s.name, s.nameAr)}</strong>
                    <div className="muted" style={{ fontSize: 12 }}><bdi>{s.code}</bdi></div>
                  </td>
                  <td style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {s.publishedVersion
                      ? <span className="pill pill-live">{t('builder.live', { n: s.publishedVersion })}</span>
                      : <span className="pill">{t('builder.notLive')}</span>}
                    {s.draftVersion && <span className="pill pill-draft">{t('builder.draft', { n: s.draftVersion })}</span>}
                  </td>
                  <td>{s.workflowVersion === 2 ? <span className="pill pill-ai">✨ {t('process.v2')}</span> : t('process.v1')}</td>
                  <td>
                    {formatDate(s.updatedAt, true)}
                    <div className="muted" style={{ fontSize: 12 }}>{t('builder.by', { name: s.updatedBy })}</div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
