import { useEffect, useState } from 'react';
import {
  Alert, DynamicForm, formatDate, formatMoney, openBlob,
  type Application, type Payment, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';

const journey = [
  { status: 'Submitted', label: 'Submitted' },
  { status: 'UnderReview', label: 'Ministry review' },
  { status: 'AwaitingPayment', label: 'Payment' },
  { status: 'Approved', label: 'Certificate issued' },
] as const;

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

  // The workflow moves on asynchronously after payment; poll briefly until the certificate appears.
  useEffect(() => {
    if (app.status !== 'Paid' && !(app.status === 'AwaitingPayment' && payment?.status === 'Paid')) return;
    const t = setInterval(onRefresh, 2000);
    return () => clearInterval(t);
  }, [app.status, payment?.status, onRefresh]);

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

  const order = ['Submitted', 'UnderReview', 'AwaitingPayment', 'Paid', 'Approved'];
  const position = app.status === 'Rejected' ? 1 : order.indexOf(app.status);

  return (
    <div className="split">
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {app.status === 'Rejected' && (
          <Alert kind="error"><strong>Not approved.</strong> {app.decisionComment}</Alert>
        )}
        {app.status === 'AwaitingPayment' && payment?.status !== 'Paid' && (
          <div className="card">
            <h2>Your application was approved</h2>
            <p>Pay the fee to receive your certificate.</p>
            {error && <Alert kind="error">{error}</Alert>}
            <dl className="kv" style={{ marginBottom: 16 }}>
              <dt>Amount</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
              <dt>For</dt><dd>{payment?.description ?? service.name}</dd>
            </dl>
            <button className="btn btn-primary" disabled={busy || !app.paymentId} onClick={pay}>
              {busy ? 'Processing…' : `Pay ${formatMoney(app.fee, app.currency)}`}
            </button>
            <p className="muted" style={{ fontSize: 13 }}>Test environment: payment is simulated, no card is charged.</p>
          </div>
        )}
        {(app.status === 'Paid' || (app.status === 'AwaitingPayment' && payment?.status === 'Paid')) && (
          <Alert kind="info">Payment received ({payment?.transactionReference}). Issuing your certificate…</Alert>
        )}
        {app.status === 'Approved' && (
          <div className="card">
            <h2>Your certificate is ready</h2>
            <p className="muted">Issued {formatDate(app.completedAt)}.</p>
            <button className="btn btn-primary" onClick={downloadCertificate}>Download certificate</button>
          </div>
        )}
        <div className="card">
          <h2>Application details</h2>
          {service.steps.map(s => (
            <section key={s.id} style={{ marginBottom: 12 }}>
              <h3>{s.title}</h3>
              <DynamicForm step={{ ...s, description: null }} values={app.data} readOnly
                onOpenFile={async id => openBlob(await api.download(`/api/documents/documents/${id}/content`))} />
            </section>
          ))}
        </div>
      </div>
      <aside className="card">
        <h3>Progress</h3>
        <ul className="timeline">
          {journey.map((j, i) => {
            const idx = order.indexOf(j.status);
            const cls = app.status === 'Rejected'
              ? (i === 0 ? 'done' : i === 1 ? 'current' : '')
              : idx < position || app.status === 'Approved' ? 'done' : idx === position ? 'current' : '';
            return <li key={j.status} className={cls}>{j.label}</li>;
          })}
        </ul>
        <dl className="kv" style={{ marginTop: 8 }}>
          <dt>Submitted</dt><dd>{formatDate(app.submittedAt, true)}</dd>
          <dt>Fee</dt><dd>{formatMoney(app.fee, app.currency)}</dd>
        </dl>
      </aside>
    </div>
  );
}
