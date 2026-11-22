import { useCallback, useEffect, useState } from 'react';
import { useParams } from '@tanstack/react-router';
import { ApiError, getLang, loc, t, type Application, type ServiceDefinition } from '@dsp/core';
import {
  AegovTab, AegovTabPanel, AegovTabs, ApplicationSummary, Loading, Notice, PageHeader, Panel, SplitLayout, StatusBadge,
} from '@dsp/ui';
import { api } from '../session';
import { FormView } from './FormView';
import { ChatView } from './ChatView';
import { StatusView } from './StatusView';

/** One application: fill it in (form or chat), answer an information request, or follow its progress. */
export function ApplicationPage() {
  const { id } = useParams({ strict: false }) as { id: string };
  const [app, setApp] = useState<Application>();
  const [service, setService] = useState<ServiceDefinition>();
  const [error, setError] = useState<string>();

  const load = useCallback(async () => {
    try {
      const a = await api.get<Application>(`/api/v1/eservices/applications/${id}`);
      setApp(a);
      // The version the application was started on, so the form matches what was filled in.
      if (!service) setService(await api.get<ServiceDefinition>(`/api/v1/eservices/services/${a.serviceCode}?version=${a.serviceVersion}`));
    } catch (e) {
      setError(e instanceof ApiError && e.status === 404 ? t('app.notFound') : (e as Error).message);
    }
  }, [id, service]);

  useEffect(() => { load(); }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  if (error) return <Notice kind="error">{error}</Notice>;
  if (!app || !service) return <Loading />;

  const isDraft = app.status === 'Draft';
  const infoRequested = app.status === 'InformationRequested';
  const required = service.steps.flatMap(s => s.fields).filter(f => f.required);
  const filled = required.filter(f => !!app.data[f.key]).length;

  const header = (
    <PageHeader overline={<bdi>{app.number}</bdi>} title={loc(service.name, service.nameAr)} badges={<StatusBadge status={app.status} />} />
  );

  if (!isDraft && !infoRequested) {
    return <>{header}<StatusView app={app} service={service} onRefresh={load} /></>;
  }

  return (
    <>
      {header}
      {infoRequested && (
        <Notice kind="warning" title={t('app.infoNeeded')}>
          <p>{t('app.infoNeededIntro')}</p>
          <ul className="my-2 ps-5">{app.informationRequest.map((q, i) => <li key={i}>{getLang() === 'ar' ? q.ar : q.en}</li>)}</ul>
          <p>{t('app.infoNeededHow')}</p>
        </Notice>
      )}
      <SplitLayout
        main={infoRequested
          ? <FormView app={app} service={service} onChange={setApp} resubmit />
          : (
            <AegovTabs variant="segmented" labelTablist={t('app.howToApply')}>
              <AegovTab slot="tabs" tabId="form">{t('app.form')}</AegovTab>
              <AegovTab slot="tabs" tabId="chat">{t('app.chat')}</AegovTab>
              <AegovTabPanel tabId="form"><div className="pt-4"><FormView app={app} service={service} onChange={setApp} /></div></AegovTabPanel>
              <AegovTabPanel tabId="chat">
                <div className="pt-4">
                  <ChatView app={app} onApplication={partial => (partial.status !== 'Draft' ? load() : setApp({ ...app, ...partial }))} />
                </div>
              </AegovTabPanel>
            </AegovTabs>
          )}
        aside={(
          <Panel title={t('app.yours')}>
            <ApplicationSummary app={app} extra={[{ label: t('app.progress'), value: t('app.progressValue', { filled, total: required.length }) }]} />
            <p className="mt-4 text-sm text-aeblack-600">{t('app.sideNote')}</p>
          </Panel>
        )} />
    </>
  );
}
