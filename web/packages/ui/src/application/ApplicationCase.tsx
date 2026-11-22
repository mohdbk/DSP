import type { ReactNode } from 'react';
import { loc, t, type AiAssessment, type Application, type ServiceDefinition } from '@dsp/core';
import { AiReviewPanel } from '../ai/AiReviewPanel';
import { PageHeader, Panel, SplitLayout } from '../layout/Page';
import { StatusBadge } from '../feedback/Feedback';
import { ServiceAnswers } from '../forms/ServiceForm';
import { ApplicationSummary } from './ApplicationViews';

/** One application as an officer sees it: answers, key facts, the AI pre-review and page-specific side panels. */
export function ApplicationCase({ app, service, assessments, actions, summaryExtra, onOpenFile, children }: {
  app: Application;
  service: ServiceDefinition;
  assessments?: AiAssessment[];
  actions?: ReactNode;
  summaryExtra?: { label: ReactNode; value: ReactNode }[];
  onOpenFile: (documentId: string) => void;
  /** Extra side panels, e.g. the decision. */
  children?: ReactNode;
}) {
  return (
    <>
      <PageHeader overline={<bdi>{app.number}</bdi>} title={loc(service.name)}
        badges={<StatusBadge status={app.status} />} actions={actions} />
      <SplitLayout
        main={(
          <Panel title={t('status.details')}>
            <ServiceAnswers steps={service.steps} values={app.data} onOpenFile={onOpenFile} />
          </Panel>
        )}
        aside={(
          <>
            <Panel title={t('task.application')}><ApplicationSummary app={app} extra={summaryExtra} /></Panel>
            <AiReviewPanel assessments={assessments} workflowVersion={app.workflowVersion} />
            {children}
          </>
        )} />
    </>
  );
}
