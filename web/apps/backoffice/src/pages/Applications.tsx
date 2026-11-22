import { useEffect, useMemo, useState } from 'react';
import { getRouteApi, useNavigate, useParams } from '@tanstack/react-router';
import { formatDate, loc, openDocument, t, type Application, type ApplicationStatus } from '@dsp/core';
import {
  AegovButton, ApplicationCase, ArrowLeft, type ColumnDef, DataTable, IconLabel, listStateProps, Loading, Notice, PageHeader, StatusBadge,
} from '@dsp/ui';
import { api } from '../session';
import { useCase } from './useCase';

const route = getRouteApi('/applications');

export function ApplicationsPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [apps, setApps] = useState<Application[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    api.list<Application>('/api/v1/eservices/applications?all=true').then(setApps).catch(e => setError(e.message));
  }, []);

  const columns = useMemo<ColumnDef<Application, string>[]>(() => [
    { id: 'number', header: t('number'), accessorFn: a => a.number, cell: c => <strong><bdi>{c.getValue()}</bdi></strong> },
    { id: 'service', header: t('service'), accessorFn: a => loc(a.serviceName), meta: { filter: true } },
    { id: 'applicant', header: t('applicant'), accessorFn: a => a.applicantName },
    { id: 'reference', header: t('reference'), accessorFn: a => a.referenceValue ?? '', cell: c => c.getValue() ? <bdi>{c.getValue()}</bdi> : '—' },
    {
      id: 'process', header: t('workflow'), accessorFn: a => String(a.workflowVersion),
      cell: c => t(`process.v${c.getValue()}`),
      meta: { filter: true, filterLabel: v => t(`process.v${v}`) },
    },
    {
      id: 'status', header: t('status'), accessorFn: a => a.status, cell: c => <StatusBadge status={c.getValue() as ApplicationStatus} />,
      meta: { filter: true, filterLabel: v => t(`status.${v}`) },
    },
    { id: 'updated', header: t('updated'), accessorFn: a => a.updatedAt, cell: c => formatDate(c.getValue(), true) },
  ], []);

  return (
    <>
      <PageHeader title={t('apps.title')} />
      {error && <Notice kind="error">{error}</Notice>}
      {!apps && !error && <Loading />}
      {apps && (
        <DataTable label={t('apps.title')} data={apps} columns={columns} getRowId={a => a.id}
          onRowAction={a => navigate({ to: '/applications/$id', params: { id: a.id } })}
          {...listStateProps(search, next => navigate({ to: '/applications', search: next, replace: true }))}
          emptyTitle={t('apps.empty')} />
      )}
    </>
  );
}

export function ApplicationDetailPage() {
  const { id } = useParams({ strict: false }) as { id: string };
  const navigate = useNavigate();
  const { app, service, assessments, error } = useCase(id);

  if (error) return <Notice kind="error">{error}</Notice>;
  if (!app || !service) return <Loading />;

  return (
    <ApplicationCase app={app} service={service} assessments={assessments} onOpenFile={docId => openDocument(api, docId)}
      summaryExtra={[
        { label: t('decision'), value: app.approved == null ? '—' : app.approved ? t('approved') : t('rejected') },
        ...(app.decidedByAi ? [{ label: t('apps.decidedBy'), value: t('apps.decidedByAi') }] : []),
        ...(app.decisionComment ? [{ label: t('comment'), value: app.decisionComment }] : []),
        { label: t('completed'), value: formatDate(app.completedAt, true) || '—' },
      ]}
      actions={(
        <>
          {app.certificateDocumentId && (
            <AegovButton appearance="outline" onAegovButtonClick={() => openDocument(api, app.certificateDocumentId!)}>{t('apps.viewCertificate')}</AegovButton>
          )}
          <AegovButton appearance="link" onAegovButtonClick={() => navigate({ to: '/applications' })}><IconLabel icon={ArrowLeft} mirror>{t('apps.back')}</IconLabel></AegovButton>
        </>
      )} />
  );
}
