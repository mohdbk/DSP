import { useEffect, useState } from 'react';
import { formatDate, formatMoney, loc, openDocument, t, type Application, type Payment, type ServiceDefinition, usesAi } from '@dsp/core';
import {
  AegovButton, ApplicationJourney, ApplicationSummary, CreditCard, DescriptionList, DownloadSimple, IconLabel, Money, Notice, Panel, ServiceAnswers, SplitLayout,
} from '@dsp/ui';
import { api } from '../session';

/** A submitted application: what happens next (AI check, payment, certificate) and what was submitted. */
export function StatusView({ app, service, onRefresh }: {
  app: Application;
  service: ServiceDefinition;
  onRefresh: () => void;
}) {
  const [payment, setPayment] = useState<Payment>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (app.paymentId) api.get<Payment>(`/api/v1/payments/payments/${app.paymentId}`).then(setPayment).catch(() => {});
  }, [app.paymentId]);

  // The workflow moves on asynchronously (AI pre-review, certificate after payment); poll until it does.
  const paid = payment?.status === 'Paid';
  const waiting = app.status === 'Submitted' || app.status === 'AiReview' || app.status === 'Paid' ||
    (app.status === 'AwaitingPayment' && paid);
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
      setPayment(await api.post<Payment>(`/api/v1/payments/payments/${app.paymentId}/pay`));
      setTimeout(onRefresh, 1500);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const amount = formatMoney(app.fee, app.currency);

  return (
    <SplitLayout
      main={(
        <>
          {app.status === 'Rejected' && <Notice kind="error" title={t('status.notApproved')}>{app.decisionComment}</Notice>}
          {(app.status === 'AiReview' || (app.status === 'Submitted' && usesAi(app.workflowVersion))) && (
            <Notice kind="info">{t('status.aiChecking')}</Notice>
          )}
          {app.status === 'AwaitingPayment' && !paid && (
            <Panel title={t('status.approvedTitle')}>
              <p className="mb-4">{t('status.payToReceive')}</p>
              {error && <Notice kind="error">{error}</Notice>}
              <DescriptionList items={[
                { label: t('status.amount'), value: <Money>{amount}</Money> },
                { label: t('status.for'), value: loc(payment?.description) || loc(service.name) },
              ]} />
              <div className="mt-5">
                <AegovButton isLoading={busy} isDisabled={!app.paymentId} onAegovButtonClick={pay}><IconLabel icon={CreditCard}>{t('status.pay', { amount })}</IconLabel></AegovButton>
              </div>
              <p className="mt-3 text-sm text-aeblack-600">{t('status.simulated')}</p>
            </Panel>
          )}
          {(app.status === 'Paid' || (app.status === 'AwaitingPayment' && paid)) && (
            <Notice kind="info">{t('status.paymentReceived', { ref: payment?.transactionReference ?? '' })}</Notice>
          )}
          {app.status === 'Approved' && app.certificateDocumentId && (
            <Panel title={t('status.certificateReady')}>
              <p className="mb-4 text-aeblack-600">{t('status.issued', { date: formatDate(app.completedAt) })}</p>
              <AegovButton onAegovButtonClick={() => openDocument(api, app.certificateDocumentId!, `certificate-${app.number}.html`)}>
                <IconLabel icon={DownloadSimple}>{t('status.download')}</IconLabel>
              </AegovButton>
            </Panel>
          )}
          <Panel title={t('status.details')}>
            <ServiceAnswers steps={service.steps} values={app.data} onOpenFile={id => openDocument(api, id)} />
          </Panel>
        </>
      )}
      aside={(
        <>
          <Panel title={t('app.progress')}><ApplicationJourney app={app} /></Panel>
          <Panel><ApplicationSummary app={app} /></Panel>
        </>
      )} />
  );
}
