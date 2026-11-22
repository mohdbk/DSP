import { useEffect, useState } from 'react';
import { AegovButton } from '@aegov/designsystem-react';
import { t } from '@dsp/core';
import { TextField } from '../forms/fields';
import { Modal } from '../feedback/Modal';
import { Notice } from '../feedback/Feedback';
import { BookmarkSimple, Icon, IconLabel, Trash } from '../icons';
import { useApi } from './ApiContext';
import { sanitizeFilters, type FilterState } from './filtering';

/** A list's state that a view keeps. */
export interface ViewState { q?: string; sort?: string; filter?: FilterState }

interface SavedView { id: string; list: string; name: string; state: ViewState }

const base = '/api/v1/platform/views';

/**
 * Each person's saved views of a list (search, sort and filters), stored on the server so they follow the person to
 * any device: save the current view under a name, apply or delete one. Shared links carry the same state in the URL.
 */
export function SavedViews({ listKey, current, onApply }: {
  /** Which list, e.g. "backoffice.applications". */
  listKey: string;
  current: ViewState;
  onApply: (view: ViewState) => void;
}) {
  const api = useApi();
  const [open, setOpen] = useState(false);
  const [views, setViews] = useState<SavedView[]>([]);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const canSave = !!(current.q || current.sort || current.filter?.conditions.length);

  useEffect(() => {
    api.get<SavedView[]>(`${base}?list=${encodeURIComponent(listKey)}`)
      .then(list => setViews(list.map(v => ({ ...v, state: { ...v.state, filter: sanitizeFilters(v.state?.filter) } }))))
      .catch(() => setViews([]));
  }, [api, listKey]);

  const run = async (work: () => Promise<void>) => {
    setBusy(true); setError(undefined);
    try { await work(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  };

  const save = () => run(async () => {
    const saved = await api.post<SavedView>(base, { list: listKey, name: name.trim(), state: current });
    setViews(list => [...list.filter(v => v.id !== saved.id), { ...saved, state: { ...saved.state, filter: sanitizeFilters(saved.state?.filter) } }]
      .sort((a, b) => a.name.localeCompare(b.name)));
    setName('');
  });

  const remove = (view: SavedView) => run(async () => {
    await api.del(`${base}/${view.id}`);
    setViews(list => list.filter(v => v.id !== view.id));
  });

  return (
    <>
      <AegovButton appearance="outline" variant="neutral-max" onAegovButtonClick={() => setOpen(true)}>
        <IconLabel icon={BookmarkSimple}>{views.length ? t('table.viewsCount', { n: views.length }) : t('table.views')}</IconLabel>
      </AegovButton>
      <Modal open={open} title={t('table.views')} onClose={() => setOpen(false)}
        actions={<AegovButton appearance="outline" variant="neutral-max" onAegovButtonClick={() => setOpen(false)}>{t('close')}</AegovButton>}>
        {error && <Notice kind="error">{error}</Notice>}
        {views.length === 0
          ? <p className="mb-4 text-aeblack-600">{t('table.noViews')}</p>
          : (
            <ul className="m-0 mb-6 flex list-none flex-col divide-y divide-aeblack-100 p-0">
              {views.map(v => (
                <li key={v.id} className="flex items-center justify-between gap-3 py-2">
                  <AegovButton appearance="link" onAegovButtonClick={() => { onApply(v.state); setOpen(false); }}>{v.name}</AegovButton>
                  <button type="button" aria-label={t('table.deleteView', { name: v.name })} title={t('table.deleteView', { name: v.name })}
                    disabled={busy} onClick={() => remove(v)}
                    className="flex size-10 items-center justify-center rounded-lg text-aered-600 hover:bg-aered-50 focus-visible:outline-2 focus-visible:outline-primary-600">
                    <Icon icon={Trash} />
                  </button>
                </li>
              ))}
            </ul>
          )}
        <div className="border-t border-aeblack-100 pt-4">
          <TextField id={`${listKey}-view-name`} label={t('table.viewName')} hint={canSave ? t('table.viewNameHint') : t('table.nothingToSave')}
            value={name} disabled={!canSave || busy} onChange={setName} />
          <AegovButton isDisabled={!canSave || !name.trim() || busy} onAegovButtonClick={save}>{t('table.saveView')}</AegovButton>
        </div>
      </Modal>
    </>
  );
}
