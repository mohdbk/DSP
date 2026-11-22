import { useEffect, useMemo, useState } from 'react';
import { getRouteApi, Link, useNavigate } from '@tanstack/react-router';
import { formatDate, loc, t, type Application, type ApplicationStatus } from '@dsp/core';
import { DataTable, listStateProps, Loading, Notice, PageHeader, StatusBadge, type ColumnDef } from '@dsp/ui';
import { api } from '../session';

const route = getRouteApi('/applications');

export function MyApplicationsPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [apps, setApps] = useState<Application[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    api.list<Application>('/api/v1/eservices/applications').then(setApps).catch(e => setError(e.message));
  }, []);

  const columns = useMemo<ColumnDef<Application, string>[]>(() => [
    { id: 'number', header: t('number'), accessorFn: a => a.number, cell: c => <strong><bdi>{c.getValue()}</bdi></strong> },
    { id: 'service', header: t('service'), accessorFn: a => loc(a.serviceName), meta: { filter: true } },
    { id: 'reference', header: t('reference'), accessorFn: a => a.referenceValue ?? '', cell: c => c.getValue() ? <bdi>{c.getValue()}</bdi> : '—' },
    {
      id: 'status', header: t('status'), accessorFn: a => a.status,
      cell: c => <StatusBadge status={c.getValue() as ApplicationStatus} />,
      meta: { filter: true, filterLabel: v => t(`status.${v}`) },
    },
    { id: 'updated', header: t('lastUpdate'), accessorFn: a => a.updatedAt, cell: c => formatDate(c.getValue(), true) },
  ], []);

  return (
    <>
      <PageHeader title={t('my.title')} />
      {error && <Notice kind="error">{error}</Notice>}
      {!apps && !error && <Loading />}
      {apps && (
        <DataTable label={t('my.title')} data={apps} columns={columns} getRowId={a => a.id}
          onRowAction={a => navigate({ to: '/applications/$id', params: { id: a.id } })}
          {...listStateProps(search, next => navigate({ to: '/applications', search: next, replace: true }))}
          emptyTitle={t('my.empty')} emptyHint={<><Link to="/">{t('my.browse')}</Link> {t('my.toStart')}</>} />
      )}
    </>
  );
}
