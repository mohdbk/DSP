import { useEffect, useMemo, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { ApiError, formatDate, loc, refreshI18n, t, type FieldError, type LanguageAdmin, type LocalizedText } from '@dsp/core';
import {
  Actions, AegovButton, CheckboxField, ChoiceField, DataTable, listStateProps, Loading, MultilingualField, Notice, NumberField,
  ownName, PageHeader, Panel, Tag, TextField, type ColumnDef,
} from '@dsp/ui';
import { api } from '../../session';
import { SettingsNav } from './SettingsNav';

const route = getRouteApi('/settings/languages');
const base = '/api/v1/platform/admin/languages';

interface Draft {
  code: string; name: LocalizedText | null; direction: 'ltr' | 'rtl'; sortOrder: number;
  showInPortal: boolean; showInBackoffice: boolean;
}

/** Where a language is offered, as a list for the tags and the multiple filter ("none" when hidden from both). */
const shownIn = (l: LanguageAdmin): string[] =>
  [...(l.showInPortal ? ['portal'] : []), ...(l.showInBackoffice ? ['backoffice'] : [])].concat(l.showInPortal || l.showInBackoffice ? [] : ['none']);
const shownVariant: Record<string, 'aegold' | 'seablue' | 'slate'> = { portal: 'aegold', backoffice: 'seablue', none: 'slate' };

/** Languages: add, rename (in every language), order, enable, disable, make default and delete. */
export function LanguagesPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  const [languages, setLanguages] = useState<LanguageAdmin[]>();
  const [selected, setSelected] = useState<string | 'new'>();
  const [draft, setDraft] = useState<Draft>();
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [message, setMessage] = useState<{ kind: 'success' | 'error'; text: string }>();
  const [busy, setBusy] = useState<string>();
  const [confirmDelete, setConfirmDelete] = useState(false);

  const load = () => api.get<LanguageAdmin[]>(base).then(setLanguages).catch(e => setMessage({ kind: 'error', text: e.message }));
  useEffect(() => { void load(); }, []);

  const current = languages?.find(l => l.code === selected);
  const select = (code: string | 'new') => {
    setSelected(code);
    setErrors([]);
    setMessage(undefined);
    setConfirmDelete(false);
    const l = languages?.find(x => x.code === code);
    setDraft(l
      ? { code: l.code, name: l.name, direction: l.direction, sortOrder: l.sortOrder, showInPortal: l.showInPortal, showInBackoffice: l.showInBackoffice }
      : { code: '', name: null, direction: 'ltr', sortOrder: (languages?.length ?? 0) * 10, showInPortal: false, showInBackoffice: true });
  };

  async function run(kind: string, action: () => Promise<unknown>, success: string) {
    setBusy(kind);
    setMessage(undefined);
    try {
      await action();
      await load();
      await refreshI18n(); // the portal's own language list and texts follow at once
      setMessage({ kind: 'success', text: success });
      setErrors([]);
    } catch (e) {
      if (e instanceof ApiError && e.errors.length) setErrors(e.errors);
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally {
      setBusy(undefined);
    }
  }

  const err = (field: string) => errors.find(e => e.field === field)?.message;
  const body = () => ({
    code: draft!.code.trim(), name: draft!.name ?? {}, direction: draft!.direction, sortOrder: draft!.sortOrder,
    showInPortal: draft!.showInPortal, showInBackoffice: draft!.showInBackoffice,
  });

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns = useMemo<ColumnDef<LanguageAdmin, any>[]>(() => [
    {
      id: 'language', header: t('settings.language'), accessorFn: l => `${ownName(l)} ${loc(l.name)}`,
      cell: c => (
        <span>
          <strong lang={c.row.original.code} dir={c.row.original.direction}>{ownName(c.row.original)}</strong>
          <span className="block text-xs text-aeblack-500">{loc(c.row.original.name)} · <bdi>{c.row.original.code}</bdi></span>
        </span>
      ),
    },
    {
      id: 'status', header: t('status'),
      accessorFn: l => (l.isDefault ? 'default' : l.isEnabled ? 'enabled' : 'disabled'),
      cell: c => <Tag variant={c.getValue() === 'disabled' ? 'slate' : c.getValue() === 'default' ? 'aegold' : 'aegreen'}>{t(`settings.lang.${c.getValue()}`)}</Tag>,
      meta: { filter: true, filterLabel: v => t(`settings.lang.${v}`) },
    },
    {
      id: 'shownIn', header: t('settings.shownIn'), accessorFn: shownIn, enableSorting: false,
      cell: c => (
        <span className="flex flex-wrap gap-1">
          {c.getValue<string[]>().map(v => <Tag key={v} variant={shownVariant[v]}>{t(`settings.shownTag.${v}`)}</Tag>)}
        </span>
      ),
      meta: { filter: true, multiple: true, filterOptions: ['portal', 'backoffice', 'none'], filterLabel: v => t(`settings.shownTag.${v}`) },
    },
    {
      id: 'direction', header: t('settings.direction'), accessorFn: l => l.direction,
      cell: c => t(`settings.dir.${c.getValue()}`), meta: { filter: true, filterLabel: v => t(`settings.dir.${v}`) },
    },
    {
      id: 'completion', header: t('settings.completion'),
      accessorFn: l => String(l.total ? Math.round((l.translated / l.total) * 100) : 0).padStart(3, '0'),
      cell: c => `${Number(c.getValue())}% (${c.row.original.translated}/${c.row.original.total})`,
    },
    { id: 'order', header: t('settings.order'), accessorFn: l => String(l.sortOrder).padStart(5, '0'), cell: c => c.row.original.sortOrder },
    { id: 'updated', header: t('updated'), accessorFn: l => l.updatedAt, cell: c => `${formatDate(c.getValue(), true)} · ${c.row.original.updatedBy}` },
  ], []);

  if (!languages) return message ? <Notice kind="error">{message.text}</Notice> : <Loading />;

  return (
    <>
      <PageHeader title={t('settings.languages')} intro={t('settings.languagesIntro')} actions={<SettingsNav current="languages" />} />
      {message && <Notice kind={message.kind}>{message.text}</Notice>}

      {draft && (
        <Panel className="mb-6 block" title={selected === 'new' ? t('settings.addLanguage') : t('settings.editLanguage', { language: current ? ownName(current) : '' })}>
          <div className="grid gap-x-4 md:grid-cols-3">
            <TextField id="code" label={t('settings.code')} hint={t('settings.codeHint')} dir="ltr" required
              disabled={selected !== 'new'} value={draft.code} error={err('code')}
              onChange={v => setDraft({ ...draft, code: v.trim() })} />
            <ChoiceField id="direction" label={t('settings.direction')} value={draft.direction} error={err('direction')}
              choices={[{ value: 'ltr', label: t('settings.dir.ltr') }, { value: 'rtl', label: t('settings.dir.rtl') }]}
              onChange={v => setDraft({ ...draft, direction: v as 'ltr' | 'rtl' })} />
            <NumberField id="order" label={t('settings.order')} value={draft.sortOrder}
              onChange={v => setDraft({ ...draft, sortOrder: v ?? 0 })} />
          </div>
          <MultilingualField id="name" label={t('settings.languageName')} hint={t('settings.languageNameHint')} value={draft.name}
            error={c => err(`name.${c}`) ?? err('name')} onChange={v => setDraft({ ...draft, name: v })} />
          <fieldset className="mb-5">
            <legend className="mb-2 font-semibold text-aeblack-800">{t('settings.shownIn')}</legend>
            <CheckboxField id="show-portal" label={t('settings.showInPortal')} disabled={current?.isDefault}
              checked={draft.showInPortal} onChange={v => setDraft({ ...draft, showInPortal: v })} />
            <CheckboxField id="show-backoffice" label={t('settings.showInBackoffice')} disabled={current?.isDefault}
              checked={draft.showInBackoffice} onChange={v => setDraft({ ...draft, showInBackoffice: v })} />
            <p className="text-sm text-aeblack-600">{t(current?.isDefault ? 'settings.visibilityDefault' : 'settings.visibilityHint')}</p>
          </fieldset>
          {selected === 'new' && draft.code && (
            <TextField id="own-name" lang={draft.code} dir={draft.direction}
              label={t('settings.ownName')} hint={t('settings.ownNameHint')}
              value={draft.name?.[draft.code] ?? ''} onChange={v => setDraft({ ...draft, name: { ...(draft.name ?? {}), [draft.code]: v } })} />
          )}

          <Actions align="between">
            <span className="flex flex-wrap gap-3">
              {current && !current.isDefault && (
                <>
                  <AegovButton appearance="outline" isDisabled={!!busy} isLoading={busy === 'toggle'}
                    onAegovButtonClick={() => run('toggle', () => api.post(`${base}/${current.code}/${current.isEnabled ? 'disable' : 'enable'}`),
                      t(current.isEnabled ? 'settings.disabled' : 'settings.enabled'))}>
                    {t(current.isEnabled ? 'settings.disable' : 'settings.enable')}
                  </AegovButton>
                  <AegovButton appearance="outline" isDisabled={!!busy} isLoading={busy === 'default'}
                    onAegovButtonClick={() => run('default', () => api.post(`${base}/${current.code}/default`), t('settings.defaultSet'))}>
                    {t('settings.makeDefault')}
                  </AegovButton>
                  <AegovButton appearance="link" variant="danger" isDisabled={!!busy} isLoading={busy === 'delete'}
                    onAegovButtonClick={() => {
                      if (!confirmDelete) { setConfirmDelete(true); return; }
                      void run('delete', () => api.del(`${base}/${current.code}`), t('settings.deleted')).then(() => { setDraft(undefined); setSelected(undefined); });
                    }}>
                    {confirmDelete ? t('settings.deleteConfirm') : t('settings.delete')}
                  </AegovButton>
                </>
              )}
            </span>
            <span className="flex flex-wrap gap-3">
              <AegovButton appearance="link" onAegovButtonClick={() => { setDraft(undefined); setSelected(undefined); }}>{t('cancel')}</AegovButton>
              <AegovButton isDisabled={!!busy} isLoading={busy === 'save'}
                onAegovButtonClick={() => run('save', () => (selected === 'new' ? api.post(base, body()) : api.put(`${base}/${draft.code}`, body())),
                  t('settings.saved')).then(() => setSelected(draft.code))}>
                {selected === 'new' ? t('settings.addLanguage') : t('save')}
              </AegovButton>
            </span>
          </Actions>
          {selected === 'new' && <p className="mt-3 text-sm text-aeblack-600">{t('settings.newDisabledNote')}</p>}
        </Panel>
      )}

      {!draft && (
        <Actions align="start">
          <AegovButton onAegovButtonClick={() => select('new')}>{t('settings.addLanguage')}</AegovButton>
        </Actions>
      )}

      <div className="mt-6">
        <DataTable label={t('settings.languages')} data={languages} columns={columns} getRowId={l => l.code}
          onRowAction={l => select(l.code)}
          {...listStateProps(search, next => navigate({ to: '/settings/languages', search: next, replace: true }))}
          emptyTitle={t('settings.noLanguages')} />
      </div>
    </>
  );
}
