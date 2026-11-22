import { useEffect, useMemo, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import {
  ApiError, getDefaultLang, getLang, openBlob, refreshI18n, t, type LanguageAdmin,
  type TranslationRow,
} from '@dsp/core';
import {
  AegovButton, ChoiceField, type ColumnDef, ConfirmDialog, DataTable, FileField, FloppyDisk, IconLabel, listStateProps, Loading, Modal, Notice, ownName, PageHeader, Panel, Plus, Tag, TextAreaField, TextField,
} from '@dsp/ui';
import { api } from '../../session';

const route = getRouteApi('/settings/translations');

/** Where the key is used, as a list for the tags and the multiple filter ("unused" when nowhere). */
const usageOf = (r: TranslationRow): string[] => (r.usedIn.length ? r.usedIn : ['unused']);
const usageOrder = ['portal', 'backoffice', 'services', 'unused'];
const usageVariant: Record<string, 'aegold' | 'seablue' | 'camel' | 'slate'> = {
  portal: 'aegold', backoffice: 'seablue', services: 'camel', unused: 'slate',
};

const base = '/api/v1/platform/admin';

/** All texts of both portals and the services' messages, in any language: search, fill gaps, import and export. */
export function TranslationsPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [languages, setLanguages] = useState<LanguageAdmin[]>();
  const [code, setCode] = useState<string>();
  const [missing, setMissing] = useState<number>();
  const [reloadKey, setReloadKey] = useState(0);
  const [editing, setEditing] = useState<TranslationRow & { isNew?: boolean }>();
  const [text, setText] = useState('');
  const [newKey, setNewKey] = useState('');
  const [busy, setBusy] = useState<string>();
  const [confirmRemove, setConfirmRemove] = useState(false);
  const [message, setMessage] = useState<{ kind: 'success' | 'error'; text: string }>();

  useEffect(() => {
    api.list<LanguageAdmin>(`${base}/languages`).then(list => {
      setLanguages(list);
      setCode(list.find(l => l.code === getLang() && !l.isDefault)?.code ?? list.find(l => !l.isDefault)?.code ?? getDefaultLang());
    }).catch(e => setMessage({ kind: 'error', text: e.message }));
  }, []);

  // How many keys still miss a text in this language (for the badge), counted by the server.
  useEffect(() => {
    if (!code) return;
    api.page<TranslationRow>(`${base}/translations/${encodeURIComponent(code)}`, {
      pageSize: 1, filter: { match: 'all', conditions: [{ column: 'state', op: 'anyOf', values: ['missing'] }] },
    }).then(r => setMissing(r.total)).catch(() => setMissing(undefined));
  }, [code, reloadKey]);
  const reload = async () => setReloadKey(n => n + 1);

  const language = languages?.find(l => l.code === code);
  const isDefault = !!language?.isDefault;

  async function run(kind: string, action: () => Promise<unknown>, success: string) {
    setBusy(kind);
    setMessage(undefined);
    try {
      await action();
      await reload();
      await refreshI18n();
      setMessage({ kind: 'success', text: success });
      setEditing(undefined);
      setConfirmRemove(false);
    } catch (e) {
      setMessage({ kind: 'error', text: e instanceof ApiError && e.errors.length ? e.errors[0].message : (e as Error).message });
    } finally {
      setBusy(undefined);
    }
  }

  const edit = (row: TranslationRow) => { setEditing(row); setText(row.text ?? ''); setMessage(undefined); };

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns = useMemo<ColumnDef<TranslationRow, any>[]>(() => [
    { id: 'key', header: t('settings.key'), accessorFn: r => r.key, cell: c => <code className="text-xs"><bdi>{c.getValue()}</bdi></code> },
    {
      id: 'default', header: t('settings.defaultText'), accessorFn: r => r.defaultText,
      cell: c => <span dir="auto">{c.getValue()}</span>,
    },
    {
      id: 'text', header: language ? ownName(language) : '', accessorFn: r => r.text ?? '',
      cell: c => c.getValue() ? <span lang={code} dir={language?.direction}>{c.getValue()}</span> : '—',
    },
    {
      id: 'usedIn', header: t('settings.usedIn'), accessorFn: usageOf, enableSorting: false,
      cell: c => (
        <span className="flex flex-wrap gap-1">
          {c.getValue<string[]>().map(u => <Tag key={u} variant={usageVariant[u] ?? 'slate'}>{t(`settings.use.${u}`)}</Tag>)}
        </span>
      ),
      meta: { filter: true, multiple: true, filterOptions: usageOrder, filterLabel: v => t(`settings.use.${v}`) },
    },
    {
      id: 'state', header: t('status'), accessorFn: r => (r.text ? 'translated' : 'missing'),
      cell: c => <Tag variant={c.getValue() === 'missing' ? 'aered' : 'aegreen'}>{t(`settings.tr.${c.getValue()}`)}</Tag>,
      meta: { filter: true, filterLabel: v => t(`settings.tr.${v}`) },
    },
  ], [language, code]);
  // Each language is its own list: its endpoint changes with the language.
  const source = code ? `${base}/translations/${encodeURIComponent(code)}` : '';

  if (!languages || !code) return message ? <Notice kind="error">{message.text}</Notice> : <Loading />;

  return (
    <>
      <PageHeader title={t('settings.translations')} intro={t('settings.translationsIntro')}
        actions={isDefault && (
          <AegovButton onAegovButtonClick={() => { setNewKey(''); setText(''); setMessage(undefined); setEditing({ key: '', defaultText: '', usedIn: [], isNew: true }); }}>
            <IconLabel icon={Plus}>{t('settings.addKey')}</IconLabel>
          </AegovButton>
        )}
        badges={missing !== undefined && <Tag variant={missing ? 'aegold' : 'aegreen'}>{t('settings.missingCount', { n: missing })}</Tag>} />
      {message && !editing && <Notice kind={message.kind}>{message.text}</Notice>}
      <p className="mb-4 text-sm text-aeblack-600">{t('settings.usedInNote')}</p>

      <Panel className="mb-6 block">
        <div className="grid items-start gap-x-4 md:grid-cols-[minmax(0,1fr)_auto]">
          <ChoiceField id="language" as="select" label={t('settings.language')} value={code}
            choices={languages.map(l => ({ value: l.code, label: `${ownName(l)} (${l.code})${l.isDefault ? ` · ${t('settings.lang.default')}` : ''}` }))}
            onChange={v => { setCode(v); setEditing(undefined); }} />
          <div className="pt-7">
            <AegovButton appearance="outline" isDisabled={!!busy} isLoading={busy === 'export'}
              onAegovButtonClick={async () => {
                setBusy('export');
                try {
                  const all = await api.get<Record<string, string>>(`${base}/translations/${encodeURIComponent(code)}/export`);
                  await openBlob(new Blob([JSON.stringify(all, null, 2)], { type: 'application/json' }), `translations-${code}.json`);
                } finally { setBusy(undefined); }
              }}>
              {t('settings.export')}
            </AegovButton>
          </div>
        </div>
        <FileField id="import" label={t('settings.import')} hint={t('settings.importHint')} accept=".json" value=""
          onUpload={async file => {
            const strings = JSON.parse(await file.text()) as Record<string, string>;
            const result = await api.post<{ added: number; updated: number; skipped: number }>(
              `${base}/translations/${encodeURIComponent(code)}/import`, strings);
            await reload();
            await refreshI18n();
            setMessage({ kind: 'success', text: t('settings.imported', { ...result }) });
            return { id: 'imported', fileName: file.name };
          }}
          onChange={() => {}} />
      </Panel>

      <Modal open={!!editing && !confirmRemove} size="lg" busy={!!busy} onClose={() => setEditing(undefined)}
        title={editing?.isNew ? t('settings.addKey') : t('settings.editText')}
        actions={editing && (
          <>
            <span className="me-auto">
              {!editing.isNew && (
                <AegovButton appearance="link" variant="danger" isDisabled={!!busy || (isDefault ? false : !editing.text)}
                  onAegovButtonClick={() => setConfirmRemove(true)}>
                  {isDefault ? t('settings.deleteKey') : t('settings.removeText')}
                </AegovButton>
              )}
            </span>
            <AegovButton appearance="outline" variant="neutral-max" isDisabled={!!busy} onAegovButtonClick={() => setEditing(undefined)}>{t('cancel')}</AegovButton>
            <AegovButton isDisabled={!!busy || !text.trim() || (editing.isNew && !newKey.trim())} isLoading={busy === 'save'}
              onAegovButtonClick={() => run('save', () => api.put(
                `${base}/translations/${encodeURIComponent(code)}/${encodeURIComponent(editing.isNew ? newKey.trim() : editing.key)}`,
                { text }), t('settings.saved'))}>
              <IconLabel icon={FloppyDisk}>{t('save')}</IconLabel>
            </AegovButton>
          </>
        )}>
        {editing && (
          <>
            {message?.kind === 'error' && <Notice kind="error">{message.text}</Notice>}
            {editing.isNew
              ? <TextField id="new-key" label={t('settings.key')} hint={t('settings.keyHint')} dir="ltr" value={newKey} onChange={setNewKey} />
              : <p className="mb-4"><code className="text-xs"><bdi>{editing.key}</bdi></code></p>}
            {!isDefault && !editing.isNew && (
              <p className="mb-4 text-aeblack-700"><span className="font-semibold">{t('settings.defaultText')}: </span><span dir="auto">{editing.defaultText}</span></p>
            )}
            <TextAreaField id="text" lang={code} dir={language?.direction} label={language ? ownName(language) : code}
              hint={t('settings.placeholdersHint')} value={text} onChange={setText} />
          </>
        )}
      </Modal>

      <ConfirmDialog open={confirmRemove} busy={busy === 'remove'}
        title={isDefault ? t('settings.deleteKey') : t('settings.removeText')}
        message={isDefault ? t('settings.deleteKeyConfirm', { key: editing?.key ?? '' }) : t('settings.removeTextConfirm')}
        confirmLabel={isDefault ? t('settings.deleteKey') : t('settings.removeText')}
        onCancel={() => setConfirmRemove(false)}
        onConfirm={() => {
          if (!editing) return;
          void run('remove', () => (isDefault
            ? api.del(`${base}/translations/keys/${encodeURIComponent(editing.key)}`)
            : api.put(`${base}/translations/${encodeURIComponent(code)}/${encodeURIComponent(editing.key)}`, { text: null })),
          t('settings.removed'));
        }} />

      <div>
        <DataTable key={code} label={t('settings.translations')} source={source} listKey="backoffice.translations" reloadKey={reloadKey}
          columns={columns} getRowId={r => r.key} onRowAction={edit} pageSize={25}
          {...listStateProps(search, next => navigate({ to: '/settings/translations', search: next, replace: true }))}
          emptyTitle={t('settings.noTexts')} />
      </div>
    </>
  );
}
