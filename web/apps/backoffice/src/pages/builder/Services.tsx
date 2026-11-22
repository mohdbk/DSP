import { useEffect, useMemo, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { formatDate, loc, t, type BuilderServiceSummary } from '@dsp/core';
import { AegovLink, DataTable, listStateProps, Loading, Notice, PageHeader, Tag, type ColumnDef } from '@dsp/ui';
import { api } from '../../session';

const route = getRouteApi('/services');

/** Every e-service, with its live and draft versions. */
export function ServicesPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [services, setServices] = useState<BuilderServiceSummary[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    api.list<BuilderServiceSummary>('/api/v1/eservices/builder/services').then(setServices).catch(e => setError(e.message));
  }, []);

  const columns = useMemo<ColumnDef<BuilderServiceSummary, string>[]>(() => [
    {
      id: 'name', header: t('service'), accessorFn: s => loc(s.name, s.nameAr),
      cell: c => (
        <span>
          <strong>{c.getValue()}</strong>
          <span className="block text-xs text-aeblack-500"><bdi>{c.row.original.code}</bdi></span>
        </span>
      ),
    },
    { id: 'category', header: t('builder.category'), accessorFn: s => s.category, meta: { filter: true } },
    {
      id: 'status', header: t('status'),
      accessorFn: s => (s.publishedVersion ? (s.draftVersion ? 'live-draft' : 'live') : 'draft'),
      cell: c => (
        <span className="flex flex-wrap gap-2">
          {c.row.original.publishedVersion
            ? <Tag variant="aegreen">{t('builder.live', { n: c.row.original.publishedVersion })}</Tag>
            : <Tag variant="slate">{t('builder.notLive')}</Tag>}
          {c.row.original.draftVersion && <Tag variant="aegold">{t('builder.draft', { n: c.row.original.draftVersion })}</Tag>}
        </span>
      ),
      meta: {
        filter: true,
        filterLabel: v => (v === 'draft' ? t('builder.notLive') : v === 'live' ? t('builder.vstatus.Published') : `${t('builder.vstatus.Published')} + ${t('builder.vstatus.Draft')}`),
      },
    },
    {
      id: 'process', header: t('builder.process'), accessorFn: s => String(s.workflowVersion),
      cell: c => (c.getValue() === '2' ? <Tag variant="techblue">{t('process.v2')}</Tag> : t('process.v1')),
      meta: { filter: true, filterLabel: v => t(v === '2' ? 'process.v2' : 'process.v1') },
    },
    {
      id: 'updated', header: t('builder.lastChange'), accessorFn: s => s.updatedAt,
      cell: c => (
        <span>
          {formatDate(c.getValue(), true)}
          <span className="block text-xs text-aeblack-500">{t('builder.by', { name: c.row.original.updatedBy })}</span>
        </span>
      ),
    },
  ], []);

  return (
    <>
      <PageHeader title={t('builder.title')} intro={t('builder.intro')}
        actions={<AegovLink href="/services/new" appearance="solid" variant="primary">{t('builder.new')}</AegovLink>} />
      {error && <Notice kind="error">{error}</Notice>}
      {!services && !error && <Loading />}
      {services && (
        <DataTable label={t('builder.title')} data={services} columns={columns} getRowId={s => s.code}
          onRowAction={s => navigate({ to: '/services/$code', params: { code: s.code } })}
          {...listStateProps(search, next => navigate({ to: '/services', search: next, replace: true }))}
          emptyTitle={t('builder.empty')} />
      )}
    </>
  );
}
