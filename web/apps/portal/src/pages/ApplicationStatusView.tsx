import { useEffect, useState } from 'react';
import {
  Alert, DynamicForm, formatDate, formatMoney, loc, openBlob, t,
  type Application, type ApplicationStatus, type Payment, type ServiceDefinition, type TKey,
} from '@dsp/shared';
import { api } from '../session';

interface Stage { key: TKey; statuses: ApplicationStatus[] }

const standardJourney: Stage[] = [
  { key: 'journey.submitted', statuses: ['Submitted'] },
  { key: 'journey.review', statuses: ['UnderReview'] },
  { key: 'journey.payment', statuses: ['AwaitingPayment', 'Paid'] },
  { key: 'journey.certificate', statuses: ['Approved'] },
];

const aiJourney: Stage[] = [
  standardJourney[0],
  { key: 'journey.aiReview', statuses: ['AiReview', 'InformationRequested'] },
  ...standardJourney.slice(1),
];

export function ApplicationStatusView({ app, service, onRefresh }: {
  app: Application;
  service: ServiceDefinition;
  onRefresh: () => void;
}) {
  const [payment, setPayment] = useState<Payment>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (app.paymentId) api.get<Payment>(`/api/payments/payments/${app.paymentId}`).then(setPayment).catch(() => {});
  }, [app.paymentId]);

  // The workflow moves on asynchronously (AI pre-review, certificate after payment); poll until it does.
  const waiting = app.status === 'Submitted' || app.status === 'AiReview' || app.status === 'Paid' ||
    (app.status === 'AwaitingPayment' && payment?.status === 'Paid');
  useEffect(() => {
    if (!waiting) return;
    const timer = setInterval(onRefresh, 2500);
    return () => clearInterval(timer);
  }, [waiting, onRefresh]);

  async function pay() {
    if (!app.paymentId) return;
    setBusy(true);
    setError(undefined);
    try {
      setPayment(await api.post<Payment>(`/api/payments/payments/${app.paymentId}/pay`));
      setTimeout(onRefresh, 1500);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function downloadCertificate() {
    if (!app.certificateDocumentId) return;
    openBlob(await api.download(`/api/documents/documents/${app.certificateDocumentId}/content`), `certificate-${app.number}.html`);
  }

  const journey = app.workflowVersion === 2 ? aiJourney : standardJourney;
  const position = app.status === 'Rejected'
    ? journey.findIndex(s => s.key === 'journey.review')
    : journey.findIndex(s => s.statuses.includes(app.status));

  return (
    <div className="split">
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {app.status === 'Rejected' && (
          <Alert kind="error"><strong>{t('status.notApproved')}</strong> {app.decisionComment}</Alert>
        )}
        {(app.status === 'AiReview' || (app.status === 'Submitted' && app.workflowVersion === 2)) && (
          <Alert kind="info">✨ {t('status.aiChecking')}</Alert>
        )}
        {app.status === 'AwaitingPayment' && payment?.status !== 'Paid' && (
          <div className="card">
            <h2>{t('status.approvedTitle')}</h2>
            <p>{t('status.payToReceive')}</p>
            {error && <Alert kind="error">{error}</Alert>}
            <dl className="kv" style={{ marginBottom: 16 }}>
              <dt>{t('status.amount')}</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
              <dt>{t('status.for')}</dt><dd>{payment?.description ?? loc(service.name, service.nameAr)}</dd>
            </dl>
            <button className="btn btn-primary" disabled={busy || !app.paymentId} onClick={pay}>
              {busy ? t('status.processing') : t('status.pay', { amount: formatMoney(app.fee, app.currency) })}
            </button>
            <p className="muted" style={{ fontSize: 13 }}>{t('status.simulated')}</p>
          </div>
        )}
        {(app.status === 'Paid' || (app.status === 'AwaitingPayment' && payment?.status === 'Paid')) && (
          <Alert kind="info">{t('status.paymentReceived', { ref: payment?.transactionReference ?? '' })}</Alert>
        )}
        {app.status === 'Approved' && (
          <div className="card">
            <h2>{t('status.certificateReady')}</h2>
            <p className="muted">{t('status.issued', { date: formatDate(app.completedAt) })}</p>
            <button className="btn btn-primary" onClick={downloadCertificate}>{t('status.download')}</button>
          </div>
        )}
        <div className="card">
          <h2>{t('status.details')}</h2>
          {service.steps.map(s => (
            <section key={s.id} style={{ marginBottom: 12 }}>
              <h3>{loc(s.title, s.titleAr)}</h3>
              <DynamicForm step={{ ...s, description: null }} values={app.data} readOnly
                onOpenFile={async id => openBlob(await api.download(`/api/documents/documents/${id}/content`))} />
            </section>
          ))}
        </div>
      </div>
      <aside className="card">
        <h3>{t('app.progress')}</h3>
        <ul className="timeline">
          {journey.map((stage, i) => {
            const cls = app.status === 'Approved' || i < position ? 'done' : i === position ? 'current' : '';
            return <li key={stage.key} className={cls}>{t(stage.key)}</li>;
          })}
        </ul>
        <dl className="kv" style={{ marginTop: 8 }}>
          <dt>{t('submitted')}</dt><dd>{formatDate(app.submittedAt, true)}</dd>
          <dt>{t('fee')}</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
          <dt>{t('workflow')}</dt><dd>{t(app.workflowVersion === 2 ? 'process.v2' : 'process.v1')}</dd>
        </dl>
      </aside>
    </div>
  );
}
