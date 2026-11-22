import type { ReactNode } from 'react';
import { AegovButton, AegovCard, AegovTimeline, AegovTimelineStep } from '@aegov/designsystem-react';
import {
  formatDate, formatMoney, loc, t, type Application, type ApplicationStatus, type Notification, type ServiceSummary,
} from '@dsp/core';
import { DescriptionList } from '../data/DescriptionList';
import { Money, Tag } from '../feedback/Feedback';
import { ArrowRight, FileText, Icon, IconLabel } from '../icons';

/** A service in the catalogue, with its fee and the button that starts an application. */
export function ServiceCard({ service, busy, disabled, onApply }: {
  service: ServiceSummary;
  busy?: boolean;
  disabled?: boolean;
  onApply: () => void;
}) {
  return (
    <AegovCard appearance="solid" variant="neutral-min" borderWidth={1} hover="standard" className="flex h-full flex-col">
      <div className="flex h-full flex-col">
      <div className="flex items-start justify-between gap-3">
        <span className="flex size-14 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary-600">
          <Icon icon={FileText} size="size-10" weight="duotone" />
        </span>
        <div className="flex flex-wrap justify-end gap-2">
          <Tag variant="aegold">{loc(service.category)}</Tag>
          {service.workflowVersion === 2 && <Tag variant="techblue">{t('catalog.aiAssisted')}</Tag>}
        </div>
      </div>
      <h2 className="mt-3 text-h5 font-bold text-aeblack-900">{loc(service.name)}</h2>
      <p className="mt-2 flex-1 text-aeblack-600">{loc(service.description)}</p>
      <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-aeblack-100 pt-4">
        <span>
          <strong><Money>{formatMoney(service.fee, service.currency)}</Money></strong>
          <span className="text-sm text-aeblack-600"> · {t('steps', { n: service.stepCount })}</span>
        </span>
        <AegovButton size="sm" isLoading={!!busy} isDisabled={!!disabled} onAegovButtonClick={onApply}><IconLabel icon={ArrowRight} mirror>{t('catalog.apply')}</IconLabel></AegovButton>
      </div>
      </div>
    </AegovCard>
  );
}

/** Key facts about an application, for the side column. Extra rows can be added by the page. */
export function ApplicationSummary({ app, extra = [] }: {
  app: Application;
  extra?: { label: ReactNode; value: ReactNode }[];
}) {
  return (
    <DescriptionList items={[
      { label: t('number'), value: <bdi>{app.number}</bdi> },
      { label: t('applicant'), value: app.applicantName },
      { label: t('submitted'), value: formatDate(app.submittedAt, true) || '—' },
      { label: t('fee'), value: <Money>{formatMoney(app.fee, app.currency)}</Money> },
      { label: t('workflow'), value: `${t(app.workflowVersion === 2 ? 'process.v2' : 'process.v1')} · v${app.serviceVersion}` },
      ...extra,
    ]} />
  );
}

interface Stage { key: string; statuses: ApplicationStatus[] }

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

/** Where an application is in its process (standard or AI-assisted). */
export function ApplicationJourney({ app }: { app: Application }) {
  const journey = app.workflowVersion === 2 ? aiJourney : standardJourney;
  const position = app.status === 'Rejected'
    ? journey.findIndex(s => s.key === 'journey.review')
    : journey.findIndex(s => s.statuses.includes(app.status));
  return (
    <AegovTimeline key={journey.length} orientation="vertical">
      {journey.map((stage, i) => (
        <AegovTimelineStep key={stage.key}
          status={app.status === 'Approved' || i < position ? 'completed' : i === position ? 'active' : 'pending'}>
          {t(stage.key)}
        </AegovTimelineStep>
      ))}
    </AegovTimeline>
  );
}

/** In-app notifications, newest first. */
export function NotificationList({ items, onOpen }: { items: Notification[]; onOpen: (n: Notification) => void }) {
  return (
    <ul className="m-0 flex list-none flex-col gap-3 p-0">
      {items.map(n => (
        <li key={n.id}>
          <AegovCard appearance="solid" variant={n.isRead ? 'neutral-min' : 'primary'} borderWidth={1} hover="standard">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                <p className="font-bold text-aeblack-900">{n.title}</p>
                <p className="text-aeblack-700">{n.body}</p>
              </div>
              <span className="text-sm text-aeblack-500">{formatDate(n.createdAt, true)}</span>
            </div>
            <div className="mt-2">
              <AegovButton appearance="link" size="sm" onAegovButtonClick={() => onOpen(n)}>
                {n.link ? t('notifications.open') : t('notifications.markRead')}
              </AegovButton>
            </div>
          </AegovCard>
        </li>
      ))}
    </ul>
  );
}
