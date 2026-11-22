import { useMemo, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { formatDate, loc, t, type BuilderServiceSummary } from '@dsp/core';
import {
  AegovButton, type ColumnDef, DataTable, IconLabel, listStateProps, PageHeader, Plus, Tag,
} from '@dsp/ui';
import { NewServiceDialog } from './NewService';

const route = getRouteApi('/services');

/** Every e-service, with its live and draft versions. */
export function ServicesPage({ creating = false }: { creating?: boolean }) {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [newOpen, setNewOpen] = useState(creating);

  const columns = useMemo<ColumnDef<BuilderServiceSummary, string>[]>(() => [
    {
      id: 'name', header: t('service'), accessorFn: s => loc(s.name),
      cell: c => (
        <span>
          <strong>{c.getValue()}</strong>
          <span className="block text-xs text-aeblack-500"><bdi>{c.row.original.code}</bdi></span>
        </span>
      ),
    },
    { id: 'category', header: t('builder.category'), accessorFn: s => loc(s.category), meta: { filter: true } },
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
      meta: { filter: true, filterLabel: v => t(`builder.state.${v}`) },
    },
    {
      id: 'process', header: t('builder.process'), accessorFn: s => String(s.workflowVersion),
      cell: c => (c.getValue() === '1' ? t('process.v1') : <Tag variant={c.getValue() === '3' ? 'aegold' : 'techblue'}>{t(`process.v${c.getValue()}`)}</Tag>),
      meta: { filter: true, filterLabel: v => t(`process.v${v}`) },
    },
    {
      id: 'updated', header: t('builder.lastChange'), accessorFn: s => s.updatedAt, meta: { type: 'date' },
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
      <PageHeader title={t('builder.pageTitle')} intro={t('builder.intro')}
        actions={<AegovButton onAegovButtonClick={() => setNewOpen(true)}><IconLabel icon={Plus}>{t('builder.new')}</IconLabel></AegovButton>} />
      <NewServiceDialog open={newOpen} onClose={() => { setNewOpen(false); if (creating) navigate({ to: '/services', replace: true }); }} />
      <DataTable label={t('builder.pageTitle')} source="/api/v1/eservices/builder/services" listKey="backoffice.services"
        columns={columns} getRowId={s => s.code}
        onRowAction={s => navigate({ to: '/services/$code', params: { code: s.code } })}
        {...listStateProps(search, next => navigate({ to: '/services', search: next, replace: true }))}
        emptyTitle={t('builder.empty')} />
    </>
  );
}
