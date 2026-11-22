import { useState } from 'react';
import { AegovButton } from '@aegov/designsystem-react';
import { t } from '@dsp/core';
import { TextField } from '../forms/fields';
import { Modal } from '../feedback/Modal';
import { BookmarkSimple, Icon, IconLabel, Trash } from '../icons';
import { sanitizeFilters, type FilterState } from './filtering';

export interface SavedView { name: string; q?: string; sort?: string; filters?: FilterState }

function load(key: string): SavedView[] {
  try {
    const list = JSON.parse(localStorage.getItem(key) ?? '[]') as SavedView[];
    return Array.isArray(list) ? list.filter(v => typeof v?.name === 'string').map(v => ({ ...v, filters: sanitizeFilters(v.filters) })) : [];
  } catch { return []; }
}

function store(key: string, views: SavedView[]) {
  try { localStorage.setItem(key, JSON.stringify(views)); } catch { /* storage unavailable: views last this session only */ }
}

/**
 * Saved views of a table (search, sort and filters) for the person using this browser: save the current view under a
 * name, apply or delete one. A convenience only; shared links carry the same state in the URL.
 */
export function SavedViews({ storageKey, current, onApply }: {
  storageKey: string;
  current: Omit<SavedView, 'name'>;
  onApply: (view: SavedView) => void;
}) {
  const [open, setOpen] = useState(false);
  const [views, setViews] = useState<SavedView[]>(() => load(storageKey));
  const [name, setName] = useState('');
  const canSave = !!(current.q || current.sort || current.filters?.conditions.length);

  const save = () => {
    const next = [...views.filter(v => v.name !== name.trim()), { ...current, name: name.trim() }];
    setViews(next); store(storageKey, next); setName('');
  };
  const remove = (viewName: string) => { const next = views.filter(v => v.name !== viewName); setViews(next); store(storageKey, next); };

  return (
    <>
      <AegovButton appearance="outline" variant="neutral-max" onAegovButtonClick={() => setOpen(true)}>
        <IconLabel icon={BookmarkSimple}>{views.length ? t('table.viewsCount', { n: views.length }) : t('table.views')}</IconLabel>
      </AegovButton>
      <Modal open={open} title={t('table.views')} onClose={() => setOpen(false)}
        actions={<AegovButton appearance="outline" variant="neutral-max" onAegovButtonClick={() => setOpen(false)}>{t('close')}</AegovButton>}>
        {views.length === 0
          ? <p className="mb-4 text-aeblack-600">{t('table.noViews')}</p>
          : (
            <ul className="m-0 mb-6 flex list-none flex-col divide-y divide-aeblack-100 p-0">
              {views.map(v => (
                <li key={v.name} className="flex items-center justify-between gap-3 py-2">
                  <AegovButton appearance="link" onAegovButtonClick={() => { onApply(v); setOpen(false); }}>{v.name}</AegovButton>
                  <button type="button" aria-label={t('table.deleteView', { name: v.name })} title={t('table.deleteView', { name: v.name })}
                    onClick={() => remove(v.name)}
                    className="flex size-10 items-center justify-center rounded-lg text-aered-600 hover:bg-aered-50 focus-visible:outline-2 focus-visible:outline-primary-600">
                    <Icon icon={Trash} />
                  </button>
                </li>
              ))}
            </ul>
          )}
        <div className="border-t border-aeblack-100 pt-4">
          <TextField id={`${storageKey}-name`} label={t('table.viewName')} hint={canSave ? t('table.viewNameHint') : t('table.nothingToSave')}
            value={name} disabled={!canSave} onChange={setName} />
          <AegovButton isDisabled={!canSave || !name.trim()} onAegovButtonClick={save}>{t('table.saveView')}</AegovButton>
        </div>
      </Modal>
    </>
  );
}
