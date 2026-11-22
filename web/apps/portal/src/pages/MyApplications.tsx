import { useMemo } from 'react';
import { getRouteApi, Link, useNavigate } from '@tanstack/react-router';
import { formatDate, loc, t, type Application, type ApplicationStatus } from '@dsp/core';
import { DataTable, listStateProps, PageHeader, StatusBadge, type ColumnDef } from '@dsp/ui';

const route = getRouteApi('/applications');

export function MyApplicationsPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const columns = useMemo<ColumnDef<Application, string>[]>(() => [
    { id: 'number', header: t('number'), accessorFn: a => a.number, cell: c => <strong><bdi>{c.getValue()}</bdi></strong> },
    { id: 'service', header: t('service'), accessorFn: a => loc(a.serviceName), meta: { filter: true } },
    { id: 'reference', header: t('reference'), accessorFn: a => a.referenceValue ?? '', cell: c => c.getValue() ? <bdi>{c.getValue()}</bdi> : '—' },
    {
      id: 'status', header: t('status'), accessorFn: a => a.status,
      cell: c => <StatusBadge status={c.getValue() as ApplicationStatus} />,
      meta: { filter: true, filterLabel: v => t(`status.${v}`) },
    },
    { id: 'updated', header: t('lastUpdate'), accessorFn: a => a.updatedAt, cell: c => formatDate(c.getValue(), true), meta: { type: 'date' } },
  ], []);

  return (
    <>
      <PageHeader title={t('my.title')} />
      <DataTable label={t('my.title')} source="/api/v1/eservices/applications" listKey="portal.applications" columns={columns} getRowId={a => a.id}
        onRowAction={a => navigate({ to: '/applications/$id', params: { id: a.id } })}
        {...listStateProps(search, next => navigate({ to: '/applications', search: next, replace: true }))}
        emptyTitle={t('my.empty')} emptyHint={<><Link to="/">{t('my.browse')}</Link> {t('my.toStart')}</>} />
    </>
  );
}
