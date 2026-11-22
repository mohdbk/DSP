import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from '@tanstack/react-router';
import { ApiError, loc, t, type BuilderService, type FieldError, type ServiceDefinition } from '@dsp/core';
import {
  AegovBadge, AegovButton, AegovLink, AegovTab, AegovTabPanel, AegovTabs, ArrowLeft, ConfirmDialog, IconLabel, Loading, Notice, PageHeader, Panel, PaperPlaneRight, Tag,
} from '@dsp/ui';
import { api } from '../../session';
import { AiReadinessPanel } from './Readiness';
import { DetailsSection, FormSection, PreviewSection, ReviewSection, VersionsSection, type EditorProps } from './editor';

const base = '/api/v1/eservices/builder';

/** Edit a service as data, preview it, and publish it as the next version. */
export function ServiceEditorPage() {
  const { code } = useParams({ strict: false }) as { code: string };
  const navigate = useNavigate();
  const [server, setServer] = useState<BuilderService>();
  const [def, setDef] = useState<ServiceDefinition>();
  const [dirty, setDirty] = useState(false);
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [busy, setBusy] = useState<'save' | 'publish' | 'discard'>();
  const [message, setMessage] = useState<{ kind: 'success' | 'error'; text: string }>();
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const [confirmPublish, setConfirmPublish] = useState(false);

  function adopt(s: BuilderService) {
    setServer(s);
    setDef(structuredClone(s.draft ?? s.published!));
    setErrors(s.draft ? s.draftErrors : []);
    setDirty(false);
  }

  useEffect(() => {
    api.get<BuilderService>(`${base}/services/${code}`).then(adopt).catch(e => setMessage({ kind: 'error', text: (e as Error).message }));
  }, [code]);

  // Live feedback while editing: the same rules that decide whether it can be published.
  const timer = useRef<number | undefined>(undefined);
  useEffect(() => {
    if (!def || !dirty) return;
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      api.post<{ errors: FieldError[] }>(`${base}/validate`, def).then(r => setErrors(r.errors)).catch(() => {});
    }, 500);
    return () => window.clearTimeout(timer.current);
  }, [def, dirty]);

  const change: EditorProps['change'] = updater => {
    setDef(prev => { const next = structuredClone(prev!); updater(next); return next; });
    setDirty(true);
    setMessage(undefined);
    setConfirmDiscard(false);
  };

  const err = useMemo(() => {
    const map = new Map(errors.map(e => [e.field, e.message]));
    return (path: string) => map.get(path);
  }, [errors]);
  const hasErr = useMemo(() => (path: string) =>
    errors.some(e => e.field === path || e.field.startsWith(`${path}.`) || e.field.startsWith(`${path}[`)), [errors]);

  async function run(kind: 'save' | 'publish' | 'discard', action: () => Promise<void>) {
    setBusy(kind);
    try { await action(); } catch (e) {
      if (e instanceof ApiError && e.errors.length) setErrors(e.errors);
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally { setBusy(undefined); }
  }

  const save = () => run('save', async () => {
    adopt(await api.put<BuilderService>(`${base}/services/${code}/draft`, def));
    setMessage({ kind: 'success', text: t('builder.saved') });
  });

  const publish = () => run('publish', async () => {
    if (dirty) await api.put<BuilderService>(`${base}/services/${code}/draft`, def);
    const result = await api.post<BuilderService>(`${base}/services/${code}/publish`);
    adopt(result);
    setMessage({ kind: 'success', text: t('builder.published', { n: result.published?.version ?? '' }) });
  });

  const discard = () => {
    run('discard', async () => {
      const remaining = await api.del<BuilderService | undefined>(`${base}/services/${code}/draft`);
      if (!remaining) { navigate({ to: '/services', replace: true }); return; }
      adopt(remaining);
      setMessage({ kind: 'success', text: t('builder.discarded') });
    }).finally(() => setConfirmDiscard(false));
  };

  if (!server || !def) return message ? <Notice kind="error">{message.text}</Notice> : <Loading />;

  const liveVersion = server.published?.version;
  const draftVersion = server.draft?.version ?? Math.max(0, ...server.versions.map(v => v.version)) + 1;
  const hasDraft = !!server.draft;
  const count = (test: (field: string) => boolean) => errors.filter(e => test(e.field)).length;
  const tabs: [string, string, number][] = [
    ['details', 'builder.tab.details', count(f => !f.startsWith('steps') && !f.startsWith('reviewChecklist'))],
    ['form', 'builder.tab.form', count(f => f.startsWith('steps'))],
    ['review', 'builder.tab.review', count(f => f.startsWith('reviewChecklist'))],
    ['preview', 'builder.tab.preview', 0],
    ['versions', 'builder.tab.versions', 0],
  ];
  const props: EditorProps = { def, change, err, hasErr };

  return (
    <>
      <PageHeader overline={<bdi>{def.code}</bdi>} title={loc(def.name, def.code)}
        actions={<AegovLink href="/services"><IconLabel icon={ArrowLeft} mirror>{t('builder.backToList')}</IconLabel></AegovLink>}
        badges={(
          <>
            {liveVersion ? <Tag variant="aegreen">{t('builder.live', { n: liveVersion })}</Tag> : <Tag variant="slate">{t('builder.notLive')}</Tag>}
            {hasDraft && <Tag variant="aegold">{t('builder.draft', { n: draftVersion })}</Tag>}
            {def.workflowVersion > 1 && <Tag variant={def.workflowVersion === 3 ? 'aegold' : 'techblue'}>{t(`process.v${def.workflowVersion}`)}</Tag>}
          </>
        )} />

      <Panel className="mb-6 block">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="text-sm text-aeblack-700">
            {dirty && <p className="font-semibold text-aegold-700">{t('builder.unsaved')}</p>}
            <p>{hasDraft || dirty
              ? t('builder.editingDraft', { n: draftVersion, live: liveVersion ? `v${liveVersion}` : t('builder.liveNone') })
              : t('builder.editingLive', { n: draftVersion, live: liveVersion ?? '' })}</p>
            <p className="mt-1">
              {errors.length > 0
                ? <Tag variant="aered">{t('builder.errors', { n: errors.length })}</Tag>
                : (hasDraft || dirty) && <Tag variant="aegreen">{t('builder.ready')}</Tag>}
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            {hasDraft && (
              <AegovButton appearance="link" variant="danger" isDisabled={!!busy} onAegovButtonClick={() => setConfirmDiscard(true)}>
                {t('builder.discard')}
              </AegovButton>
            )}
            <AegovButton appearance="outline" isDisabled={!!busy || !dirty} isLoading={busy === 'save'} onAegovButtonClick={save}>
              {t('builder.save')}
            </AegovButton>
            <AegovButton isDisabled={!!busy || errors.length > 0 || (!hasDraft && !dirty)} isLoading={busy === 'publish'} onAegovButtonClick={() => setConfirmPublish(true)}>
              <IconLabel icon={PaperPlaneRight} mirror>{t('builder.publish', { n: draftVersion })}</IconLabel>
            </AegovButton>
          </div>
        </div>
      </Panel>
      {message && <Notice kind={message.kind}>{message.text}</Notice>}

      <ConfirmDialog open={confirmDiscard} title={t('builder.discard')} message={t('builder.discardMessage')}
        confirmLabel={t('builder.discard')} busy={busy === 'discard'} onCancel={() => setConfirmDiscard(false)} onConfirm={discard} />
      <ConfirmDialog open={confirmPublish} destructive={false} title={t('builder.publish', { n: draftVersion })}
        message={t('builder.publishMessage', { n: draftVersion })} confirmLabel={t('builder.publish', { n: draftVersion })}
        busy={busy === 'publish'} onCancel={() => setConfirmPublish(false)}
        onConfirm={() => { void Promise.resolve(publish()).finally(() => setConfirmPublish(false)); }} />

      <AegovTabs isScrollable labelTablist={t('builder.pageTitle')}>
        {tabs.map(([id, label, n]) => (
          <AegovTab key={id} slot="tabs" tabId={id}>
            {t(label)}
            {/* Always rendered (hidden at zero): the tab moves slotted nodes, so React must not add or remove them. */}
            <AegovBadge slot="icon-end" variant="aered" size="sm" className={n > 0 ? undefined : 'hidden'}>{n}</AegovBadge>
          </AegovTab>
        ))}
        <AegovTabPanel tabId="details"><div className="pt-6"><DetailsSection {...props} /></div></AegovTabPanel>
        <AegovTabPanel tabId="form"><div className="pt-6"><FormSection {...props} /></div></AegovTabPanel>
        <AegovTabPanel tabId="review"><div className="pt-6"><AiReadinessPanel code={code} workflowVersion={def.workflowVersion} /><ReviewSection {...props} /></div></AegovTabPanel>
        <AegovTabPanel tabId="preview"><div className="pt-6"><PreviewSection def={def} /></div></AegovTabPanel>
        <AegovTabPanel tabId="versions"><div className="pt-6"><VersionsSection versions={server.versions} /></div></AegovTabPanel>
      </AegovTabs>
    </>
  );
}
