import { useEffect, useMemo, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { formatDate, t, type ReviewTask, type TKey } from '@dsp/core';
import { DataTable, listStateProps, Loading, Notice, PageHeader, RecommendationTag, Tag, type ColumnDef } from '@dsp/ui';
import { api } from '../session';

const route = getRouteApi('/inbox');

export function InboxPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [tasks, setTasks] = useState<ReviewTask[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    const load = () => api.list<ReviewTask>('/api/v1/eservices/tasks').then(setTasks).catch(e => setError(e.message));
    load();
    const timer = setInterval(load, 15000);
    return () => clearInterval(timer);
  }, []);

  const columns = useMemo<ColumnDef<ReviewTask, string>[]>(() => [
    { id: 'number', header: t('inbox.application'), accessorFn: x => x.applicationNumber, cell: c => <strong><bdi>{c.getValue()}</bdi></strong> },
    { id: 'service', header: t('service'), accessorFn: x => x.serviceName, meta: { filter: true } },
    { id: 'applicant', header: t('applicant'), accessorFn: x => x.applicantName },
    { id: 'reference', header: t('reference'), accessorFn: x => x.referenceValue ?? '', cell: c => c.getValue() ? <bdi>{c.getValue()}</bdi> : '—' },
    {
      id: 'ai', header: t('inbox.ai'), accessorFn: x => x.aiRecommendation ?? '',
      cell: c => <RecommendationTag value={c.getValue() || null} short />,
      meta: { filter: true, filterLabel: v => t(`ai.recShort.${v}` as TKey) },
    },
    { id: 'received', header: t('received'), accessorFn: x => x.createdAt, cell: c => formatDate(c.getValue(), true) },
    {
      id: 'due', header: t('sla'), accessorFn: x => x.dueAt,
      cell: c => new Date(c.getValue()).getTime() < Date.now()
        ? <Tag variant="aered">{t('overdue')}</Tag>
        : <span className="text-aeblack-600">{t('due', { date: formatDate(c.getValue()) })}</span>,
    },
  ], []);

  return (
    <>
      <PageHeader title={t('inbox.title')} intro={t('inbox.intro')} badges={tasks && <Tag variant="aegold">{t('inbox.open', { n: tasks.length })}</Tag>} />
      {error && <Notice kind="error">{error}</Notice>}
      {!tasks && !error && <Loading />}
      {tasks && (
        <DataTable label={t('inbox.title')} data={tasks} columns={columns} getRowId={x => x.id}
          onRowAction={x => navigate({ to: '/tasks/$id', params: { id: x.id } })}
          {...listStateProps(search, next => navigate({ to: '/inbox', search: next, replace: true }))}
          emptyTitle={t('inbox.empty')} emptyHint={t('inbox.emptyHint')} />
      )}
    </>
  );
}
