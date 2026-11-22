import { useState } from 'react';
import { AegovButton } from '@aegov/designsystem-react';
import { formatDate, t } from '@dsp/core';
import { ChoiceField, DateField, MultiChoiceField, NumberField, TextField } from '../forms/fields';
import { Modal } from '../feedback/Modal';
import { Icon, IconLabel, Plus, X } from '../icons';
import { emptyFilters, operatorsFor, valueCount, type ColumnType, type Condition, type FilterState, type Operator } from './filtering';
import type { Icon as PhosphorIcon } from '@phosphor-icons/react';

export interface FilterableColumn {
  id: string;
  label: string;
  type: ColumnType;
  /** Choices for choice columns, with their display labels. */
  options?: { value: string; label: string }[];
}

const newId = () => Math.random().toString(36).slice(2, 10);

function blankCondition(column: FilterableColumn): Condition {
  return { id: newId(), column: column.id, op: operatorsFor[column.type][0], values: column.type === 'choice' ? [] : undefined };
}

/** "Status is any of Submitted, Approved" — how a condition reads on its chip. */
export function describe(c: Condition, column: FilterableColumn | undefined): string {
  if (!column) return '';
  const label = (v: string) => column.options?.find(o => o.value === v)?.label ?? v;
  const value = (v?: string) => (column.type === 'date' && v ? formatDate(v) : v ?? '');
  const parts = valueCount(c.op) === 0 ? ''
    : valueCount(c.op) === 'many' ? (c.values ?? []).map(label).join(t('table.listSeparator'))
      : valueCount(c.op) === 2 ? t('table.betweenValues', { from: value(c.value) || '…', to: value(c.value2) || '…' })
        : c.op === 'lastDays' ? t('table.days', { n: c.value ?? '' }) : value(c.value);
  return `${column.label} ${t(`table.op.${c.op}`)}${parts ? ` ${parts}` : ''}`;
}

/** Chips for the active conditions, with remove buttons and "Clear all". */
export function FilterChips({ value, columns, onChange }: {
  value: FilterState;
  columns: FilterableColumn[];
  onChange: (next: FilterState) => void;
}) {
  if (value.conditions.length === 0) return null;
  const byId = new Map(columns.map(c => [c.id, c]));
  return (
    <div className="mb-4 flex flex-wrap items-center gap-2" aria-label={t('table.activeFilters')}>
      {value.conditions.length > 1 && (
        <span className="text-sm text-aeblack-600">{t(value.match === 'any' ? 'table.match.anyShort' : 'table.match.allShort')}</span>
      )}
      {value.conditions.map(c => (
        <span key={c.id} className="inline-flex items-center gap-1 rounded-full border border-primary-200 bg-primary-50 py-1 ps-3 pe-1 text-sm text-primary-800">
          <span>{describe(c, byId.get(c.column))}</span>
          <button type="button" aria-label={t('table.removeFilter', { filter: describe(c, byId.get(c.column)) })}
            onClick={() => onChange({ ...value, conditions: value.conditions.filter(x => x.id !== c.id) })}
            className="flex size-6 items-center justify-center rounded-full hover:bg-primary-100 focus-visible:outline-2 focus-visible:outline-primary-600">
            <Icon icon={X} size="size-5" />
          </button>
        </span>
      ))}
      <AegovButton appearance="link" size="sm" onAegovButtonClick={() => onChange(emptyFilters)}>{t('table.clearAll')}</AegovButton>
    </div>
  );
}

/** The filter editor, in a dialog: conditions on any column, each with an operator that fits the column's type. */
export function FilterDialog({ open, value, columns, onApply, onClose, icon }: {
  open: boolean;
  value: FilterState;
  columns: FilterableColumn[];
  onApply: (next: FilterState) => void;
  onClose: () => void;
  icon: PhosphorIcon;
}) {
  const [draft, setDraft] = useState<FilterState>(value);
  const [opened, setOpened] = useState(false);
  if (open && !opened) { setOpened(true); setDraft(value.conditions.length ? value : { ...value, conditions: columns[0] ? [blankCondition(columns[0])] : [] }); }
  if (!open && opened) setOpened(false);

  const byId = new Map(columns.map(c => [c.id, c]));
  const update = (id: string, patch: Partial<Condition>) =>
    setDraft(d => ({ ...d, conditions: d.conditions.map(c => (c.id === id ? { ...c, ...patch } : c)) }));
  const complete = (c: Condition) => {
    const n = valueCount(c.op);
    return n === 0 || (n === 'many' ? (c.values ?? []).length > 0 : !!c.value || !!c.value2);
  };

  return (
    <Modal open={open} title={t('table.filters')} onClose={onClose} size="xl"
      actions={(
        <>
          <AegovButton appearance="outline" variant="neutral-max" onAegovButtonClick={onClose}>{t('cancel')}</AegovButton>
          <AegovButton onAegovButtonClick={() => { onApply({ ...draft, conditions: draft.conditions.filter(complete) }); onClose(); }}>
            <IconLabel icon={icon}>{t('table.apply')}</IconLabel>
          </AegovButton>
        </>
      )}>
      {draft.conditions.length > 1 && (
        <ChoiceField id="filter-match" label={t('table.match.label')} value={draft.match}
          choices={[{ value: 'all', label: t('table.match.all') }, { value: 'any', label: t('table.match.any') }]}
          onChange={v => setDraft(d => ({ ...d, match: v as 'all' | 'any' }))} />
      )}
      <div className="flex flex-col gap-4">
        {draft.conditions.map((c, i) => {
          const column = byId.get(c.column) ?? columns[0];
          const ops = operatorsFor[column.type];
          const n = valueCount(c.op);
          const base = `filter-${c.id}`;
          return (
            <fieldset key={c.id} className="rounded-xl border border-aeblack-100 p-4">
              <legend className="px-1 text-sm font-semibold text-aeblack-700">{t('table.condition', { n: i + 1 })}</legend>
              <div className="grid gap-x-4 md:grid-cols-3">
                <ChoiceField id={`${base}-column`} as="select" label={t('table.column')} value={column.id}
                  choices={columns.map(x => ({ value: x.id, label: x.label }))}
                  onChange={v => { const next = byId.get(v); if (next) update(c.id, { ...blankCondition(next), id: c.id }); }} />
                {/* AEGOV selects move their options into their own markup: replace the controls whole when they change. */}
                <ChoiceField key={`op-${column.type}`} id={`${base}-op`} as="select" label={t('table.operator')} value={c.op}
                  choices={ops.map(o => ({ value: o, label: t(`table.op.${o}`) }))}
                  onChange={v => update(c.id, { op: v as Operator, value: undefined, value2: undefined })} />
                <div key={`value-${column.id}-${c.op}`}>
                  {n === 'many' && (
                    <MultiChoiceField id={`${base}-values`} label={t('table.value')} value={c.values ?? []}
                      choices={column.options ?? []} onChange={v => update(c.id, { values: v })} />
                  )}
                  {n !== 0 && n !== 'many' && column.type === 'date' && c.op !== 'lastDays' && (
                    <DateField id={`${base}-v1`} label={n === 2 ? t('table.from') : t('table.value')} value={c.value ?? ''}
                      onChange={v => update(c.id, { value: v })} />
                  )}
                  {c.op === 'lastDays' && (
                    <NumberField id={`${base}-v1`} label={t('table.daysLabel')} value={c.value ? Number(c.value) : null}
                      onChange={v => update(c.id, { value: v == null ? undefined : String(v) })} />
                  )}
                  {n !== 0 && n !== 'many' && column.type === 'number' && (
                    <NumberField id={`${base}-v1`} label={n === 2 ? t('table.from') : t('table.value')} value={c.value ? Number(c.value) : null}
                      onChange={v => update(c.id, { value: v == null ? undefined : String(v) })} />
                  )}
                  {n === 1 && column.type === 'text' && (
                    <TextField id={`${base}-v1`} label={t('table.value')} value={c.value ?? ''} onChange={v => update(c.id, { value: v })} />
                  )}
                </div>
              </div>
              {n === 2 && (
                <div key={`value2-${column.id}-${c.op}`} className="grid gap-x-4 md:grid-cols-3">
                  <div className="md:col-start-3">
                    {column.type === 'date'
                      ? <DateField id={`${base}-v2`} label={t('table.to')} value={c.value2 ?? ''} onChange={v => update(c.id, { value2: v })} />
                      : <NumberField id={`${base}-v2`} label={t('table.to')} value={c.value2 ? Number(c.value2) : null}
                          onChange={v => update(c.id, { value2: v == null ? undefined : String(v) })} />}
                  </div>
                </div>
              )}
              <AegovButton appearance="link" variant="danger" size="sm"
                onAegovButtonClick={() => setDraft(d => ({ ...d, conditions: d.conditions.filter(x => x.id !== c.id) }))}>
                {t('table.removeCondition')}
              </AegovButton>
            </fieldset>
          );
        })}
      </div>
      {columns.length > 0 && (
        <div className="mt-4">
          <AegovButton appearance="outline" size="sm"
            onAegovButtonClick={() => setDraft(d => ({ ...d, conditions: [...d.conditions, blankCondition(columns[0])] }))}>
            <IconLabel icon={Plus}>{t('table.addCondition')}</IconLabel>
          </AegovButton>
        </div>
      )}
    </Modal>
  );
}
