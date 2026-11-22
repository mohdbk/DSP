import { useEffect, useMemo, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { formatDate, loc, t, type ReviewTask } from '@dsp/core';
import { DataTable, listStateProps, PageHeader, RecommendationTag, Tag, type ColumnDef } from '@dsp/ui';
import { api } from '../session';

const route = getRouteApi('/inbox');
const source = '/api/v1/eservices/tasks';

export function InboxPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [open, setOpen] = useState<number>();
  const [tick, setTick] = useState(0);

  // New tasks arrive all the time: refresh the page shown and the open count every 15 seconds.
  useEffect(() => {
    const timer = setInterval(() => setTick(n => n + 1), 15000);
    return () => clearInterval(timer);
  }, []);
  useEffect(() => {
    api.page<ReviewTask>(source, { pageSize: 1 }).then(r => setOpen(r.total)).catch(() => {});
  }, [tick]);

  const columns = useMemo<ColumnDef<ReviewTask, string>[]>(() => [
    { id: 'number', header: t('inbox.application'), accessorFn: x => x.applicationNumber, cell: c => <strong><bdi>{c.getValue()}</bdi></strong> },
    { id: 'service', header: t('service'), accessorFn: x => loc(x.serviceName), meta: { filter: true } },
    { id: 'applicant', header: t('applicant'), accessorFn: x => x.applicantName },
    { id: 'reference', header: t('reference'), accessorFn: x => x.referenceValue ?? '', cell: c => c.getValue() ? <bdi>{c.getValue()}</bdi> : '—' },
    {
      id: 'ai', header: t('inbox.ai'), accessorFn: x => x.aiRecommendation ?? '',
      cell: c => <RecommendationTag value={c.getValue() || null} short />,
      meta: { filter: true, filterLabel: v => t(`ai.recShort.${v}`) },
    },
    { id: 'received', header: t('received'), accessorFn: x => x.createdAt, cell: c => formatDate(c.getValue(), true), meta: { type: 'date' } },
    {
      id: 'due', header: t('sla'), accessorFn: x => x.dueAt, meta: { type: 'date' },
      cell: c => new Date(c.getValue()).getTime() < Date.now()
        ? <Tag variant="aered">{t('overdue')}</Tag>
        : <span className="text-aeblack-600">{t('due', { date: formatDate(c.getValue()) })}</span>,
    },
  ], []);

  return (
    <>
      <PageHeader title={t('inbox.title')} intro={t('inbox.intro')} badges={open !== undefined && <Tag variant="aegold">{t('inbox.open', { n: open })}</Tag>} />
      <DataTable label={t('inbox.title')} source={source} listKey="backoffice.inbox" reloadKey={tick} columns={columns} getRowId={x => x.id}
        onRowAction={x => navigate({ to: '/tasks/$id', params: { id: x.id } })}
        {...listStateProps(search, next => navigate({ to: '/inbox', search: next, replace: true }))}
        emptyTitle={t('inbox.empty')} emptyHint={t('inbox.emptyHint')} />
    </>
  );
}
