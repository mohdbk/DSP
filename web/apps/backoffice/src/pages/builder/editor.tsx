import { useMemo, useState } from 'react';
import {
  FIELD_TYPES, formatDate, getLang, loc, PROFILE_CLAIMS, t, type FieldDefinition, type FieldType, type FormData, type FormStep,
  type LocalizedText, type ServiceDefinition, type ServiceVersionInfo,
} from '@dsp/core';
import {
  Actions, AegovButton, CheckboxField, ChoiceField, DataTable, moveItem, MultilingualField, Notice, NumberField,
  orderedLanguages, ownName, Panel, ReorderList, StepFields, StepProgress, Tag, TextAreaField, TextField, type ColumnDef,
} from '@dsp/ui';

// The sections of the service builder. Every input is a shared @dsp/ui field (AEGOV underneath);
// the step and field lists use the shared reorderable list.

export interface EditorProps {
  def: ServiceDefinition;
  change: (updater: (d: ServiceDefinition) => void) => void;
  err: (path: string) => string | undefined;
  /** True when any error is at or under the path (e.g. "steps[0].fields[1]"). */
  hasErr: (path: string) => boolean;
}

const orNull = (v: string) => (v.trim() === '' ? null : v);

/** A text in the editor's language, for new steps, fields and options (staff then fill in the other languages). */
const textNow = (text: string): LocalizedText => ({ [getLang()]: text });

export function DetailsSection({ def, change, err }: EditorProps) {
  const at = (path: string) => (code: string) => err(`${path}.${code}`) ?? err(path);
  const textFields = def.steps.flatMap(s => s.fields).filter(f => f.type === 'text' || f.type === 'email' || f.type === 'phone');
  return (
    <Panel>
      <MultilingualField id="name" label={t('builder.name')} value={def.name} error={at('name')}
        onChange={v => change(d => { d.name = v ?? {}; })} />
      <MultilingualField id="desc" multiline label={t('builder.description')} value={def.description} error={at('description')}
        onChange={v => change(d => { d.description = v ?? {}; })} />
      <MultilingualField id="cat" label={t('builder.category')} value={def.category} error={at('category')}
        onChange={v => change(d => { d.category = v ?? {}; })} />
      <div className="grid gap-x-4 md:grid-cols-3">
        <NumberField id="fee" label={t('builder.fee')} value={def.fee} error={err('fee')} onChange={v => change(d => { d.fee = v ?? 0; })} />
        <TextField id="currency" label={t('builder.currency')} dir="ltr" value={def.currency} error={err('currency')}
          onChange={v => change(d => { d.currency = v.toUpperCase().slice(0, 3); })} />
        <ChoiceField id="referenceField" as="select" label={t('builder.referenceField')} hint={t('builder.referenceHelp')}
          error={err('referenceField')} value={def.referenceField ?? ''}
          choices={[{ value: '', label: t('builder.none') }, ...textFields.map(f => ({ value: f.key, label: `${loc(f.label)} (${f.key})` }))]}
          onChange={v => change(d => { d.referenceField = v || null; })} />
      </div>
      <ChoiceField id="process" as="cards" label={t('builder.process')} error={err('workflowVersion')} value={String(def.workflowVersion)}
        choices={[
          { value: '1', label: t('process.v1'), hint: t('builder.processV1') },
          { value: '2', label: t('process.v2'), hint: t('builder.processV2Auto') },
        ]}
        onChange={v => change(d => { d.workflowVersion = Number(v); })} />
    </Panel>
  );
}

export function ReviewSection({ def, change, err, hasErr }: EditorProps) {
  const items = def.reviewChecklist ?? [];
  const listError = err('reviewChecklist') ?? (hasErr('reviewChecklist') ? t('builder.checklistErrors') : undefined);
  // One text area per language, one item per line; line N in every language is the same checklist item.
  const setLanguage = (code: string, text: string) => change(d => {
    const lines = text.split('\n');
    const next = Array.from({ length: Math.max(lines.length, items.length) }, (_, i) => {
      const item: LocalizedText = { ...(items[i] ?? {}) };
      const line = (lines[i] ?? '').trim();
      if (line) item[code] = line; else delete item[code];
      return item;
    });
    d.reviewChecklist = next.filter(item => Object.keys(item).length > 0);
  });
  return (
    <Panel>
      {def.workflowVersion !== 2 && <Notice kind="info">{t('builder.checklistV1')}</Notice>}
      {listError && <Notice kind="error">{listError}</Notice>}
      <p className="mb-4 text-sm text-aeblack-600">{t('builder.checklistHelp')}</p>
      <div className={orderedLanguages().length > 1 ? 'grid gap-x-4 md:grid-cols-2' : undefined}>
        {orderedLanguages().map(l => (
          <TextAreaField key={l.code} id={`checklist-${l.code}`} lang={l.code} dir={l.direction}
            label={t('builder.inLanguage', { label: t('builder.checklist'), language: ownName(l) })}
            value={items.map(i => i[l.code] ?? '').join('\n')} onChange={v => setLanguage(l.code, v)} />
        ))}
      </div>
    </Panel>
  );
}

const newField = (): FieldDefinition => ({
  key: `field${Date.now() % 100000}`, label: textNow(t('builder.newField')), type: 'text', required: false,
});

/** Steps and fields: reorder by dragging, select one to edit its settings. */
export function FormSection({ def, change, err, hasErr }: EditorProps) {
  const [stepIndex, setStepIndex] = useState(0);
  const [fieldIndex, setFieldIndex] = useState(0);
  const si = Math.min(stepIndex, def.steps.length - 1);
  const step = def.steps[si];
  const fi = Math.min(fieldIndex, Math.max(0, step.fields.length - 1));
  const field = step.fields[fi];
  const at = `steps[${si}]`;
  const fieldHasError = (i: number) => hasErr(`${at}.fields[${i}]`);

  const stepItems = def.steps.map((s, i) => ({
    id: `s${i}`, textValue: loc(s.title, s.id),
    content: (
      <span className="flex items-center justify-between gap-2">
        <span>{i + 1}. {loc(s.title, s.id)}</span>
        <Tag variant="slate">{s.fields.length}</Tag>
      </span>
    ),
  }));
  const fieldItems = step.fields.map((f, i) => ({
    id: `f${i}`, textValue: loc(f.label, f.key),
    content: (
      <span className="flex flex-wrap items-center justify-between gap-2">
        <span>{loc(f.label, f.key)}</span>
        <span className="flex gap-1">
          <Tag variant="slate">{t(`builder.type.${f.type}`)}</Tag>
          {fieldHasError(i) && <Tag variant="aered">!</Tag>}
        </span>
      </span>
    ),
  }));

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[20rem_minmax(0,1fr)]">
      <div className="flex flex-col gap-6">
        <Panel title={t('builder.steps')}>
          {err('steps') && <Notice kind="error">{err('steps')}</Notice>}
          <ReorderList label={t('builder.steps')} items={stepItems} selectedId={`s${si}`}
            onSelect={id => { setStepIndex(Number(id.slice(1))); setFieldIndex(0); }}
            onMove={(from, to, position) => {
              const a = Number(from.slice(1)), b = Number(to.slice(1));
              change(d => { d.steps = moveItem(d.steps, a, b, position); });
              setStepIndex(moveItem(def.steps.map((_, i) => i), a, b, position).indexOf(a));
            }} />
          <Actions align="start">
            <AegovButton appearance="outline" size="sm" onAegovButtonClick={() => {
              const n = def.steps.length + 1;
              change(d => { d.steps.push({ id: `step-${n}`, title: textNow(t('builder.stepN', { n })), fields: [newField()] }); });
              setStepIndex(def.steps.length);
              setFieldIndex(0);
            }}>{t('builder.addStep')}</AegovButton>
          </Actions>
        </Panel>
        <Panel title={t('builder.fields')}>
          {err(`${at}.fields`) && <Notice kind="error">{err(`${at}.fields`)}</Notice>}
          <ReorderList label={t('builder.fields')} items={fieldItems} selectedId={`f${fi}`}
            onSelect={id => setFieldIndex(Number(id.slice(1)))}
            onMove={(from, to, position) => {
              const a = Number(from.slice(1)), b = Number(to.slice(1));
              change(d => { d.steps[si].fields = moveItem(d.steps[si].fields, a, b, position); });
              setFieldIndex(moveItem(step.fields.map((_, i) => i), a, b, position).indexOf(a));
            }} />
          <Actions align="start">
            <AegovButton appearance="outline" size="sm" onAegovButtonClick={() => {
              change(d => { d.steps[si].fields.push(newField()); });
              setFieldIndex(step.fields.length);
            }}>{t('builder.addField')}</AegovButton>
          </Actions>
        </Panel>
      </div>

      <div className="flex flex-col gap-6">
        <StepSettings step={step} at={at} err={err} canRemove={def.steps.length > 1}
          update={fn => change(d => fn(d.steps[si]))}
          remove={() => { change(d => { d.steps.splice(si, 1); }); setStepIndex(Math.max(0, si - 1)); setFieldIndex(0); }} />
        {field && (
          <FieldSettings key={`${si}-${fi}`} field={field} at={`${at}.fields[${fi}]`} err={err} canRemove={step.fields.length > 1}
            update={fn => change(d => fn(d.steps[si].fields[fi]))}
            remove={() => { change(d => { d.steps[si].fields.splice(fi, 1); }); setFieldIndex(Math.max(0, fi - 1)); }} />
        )}
      </div>
    </div>
  );
}

function StepSettings({ step, at, err, update, remove, canRemove }: {
  step: FormStep; at: string; err: EditorProps['err']; canRemove: boolean;
  update: (fn: (s: FormStep) => void) => void; remove: () => void;
}) {
  return (
    <Panel title={t('builder.stepSettings')}
      actions={<AegovButton appearance="link" variant="danger" size="sm" isDisabled={!canRemove} onAegovButtonClick={remove}>{t('builder.remove')}</AegovButton>}>
      <TextField id={`${at}-id`} label={t('builder.stepId')} dir="ltr" value={step.id} error={err(`${at}.id`)}
        onChange={v => update(s => { s.id = v.toLowerCase().replace(/[^a-z0-9-]/g, '-'); })} />
      <MultilingualField id={`${at}-title`} label={t('builder.title')} value={step.title}
        error={code => err(`${at}.title.${code}`)} onChange={v => update(s => { s.title = v ?? {}; })} />
      <MultilingualField id={`${at}-desc`} multiline optional label={t('builder.stepDesc')} value={step.description}
        onChange={v => update(s => { s.description = v; })} />
    </Panel>
  );
}

function FieldSettings({ field, at, err, update, remove, canRemove }: {
  field: FieldDefinition; at: string; err: EditorProps['err']; canRemove: boolean;
  update: (fn: (f: FieldDefinition) => void) => void; remove: () => void;
}) {
  const isText = field.type === 'text' || field.type === 'textarea' || field.type === 'phone' || field.type === 'email';
  return (
    <Panel title={t('builder.fieldSettings')}
      actions={<AegovButton appearance="link" variant="danger" size="sm" isDisabled={!canRemove} onAegovButtonClick={remove}>{t('builder.remove')}</AegovButton>}>
      <div className="grid gap-x-4 md:grid-cols-2">
        <TextField id={`${at}-key`} label={t('builder.fieldKey')} dir="ltr" value={field.key} error={err(`${at}.key`)}
          onChange={v => update(f => { f.key = v.replace(/[^A-Za-z0-9]/g, ''); })} />
        <ChoiceField id={`${at}-type`} as="select" label={t('builder.type')} value={field.type} error={err(`${at}.type`)}
          choices={FIELD_TYPES.map(type => ({ value: type, label: t(`builder.type.${type}`) }))}
          onChange={v => update(f => {
            f.type = v as FieldType;
            if (f.type === 'select' && !f.options?.length) f.options = [{ value: 'option1', label: textNow(t('builder.optionN', { n: 1 })) }];
          })} />
      </div>
      <MultilingualField id={`${at}-label`} label={t('builder.label')} value={field.label}
        error={code => err(`${at}.label.${code}`)} onChange={v => update(f => { f.label = v ?? {}; })} />
      <CheckboxField id={`${at}-required`} label={t('builder.required')} checked={field.required}
        onChange={c => update(f => { f.required = c; })} />
      <MultilingualField id={`${at}-help`} optional label={t('builder.help')} value={field.help}
        onChange={v => update(f => { f.help = v; })} />

      {field.type === 'select' && <OptionsEditor field={field} at={at} err={err} update={update} />}

      {isText && (
        <>
          <MultilingualField id={`${at}-ph`} optional label={t('builder.placeholder')} value={field.placeholder}
            onChange={v => update(f => { f.placeholder = v; })} />
          <div className="grid gap-x-4 md:grid-cols-3">
            <NumberField id={`${at}-minl`} label={t('builder.minLength')} value={field.minLength} error={err(`${at}.minLength`)}
              onChange={v => update(f => { f.minLength = v; })} />
            <NumberField id={`${at}-maxl`} label={t('builder.maxLength')} value={field.maxLength} error={err(`${at}.maxLength`)}
              onChange={v => update(f => { f.maxLength = v; })} />
            <ChoiceField id={`${at}-prefill`} as="select" label={t('builder.prefill')} value={field.prefillFrom ?? ''}
              error={err(`${at}.prefillFrom`)}
              choices={[{ value: '', label: t('builder.none') }, ...PROFILE_CLAIMS.map(c => ({ value: c, label: t(`builder.prefill.${c}`) }))]}
              onChange={v => update(f => { f.prefillFrom = v || null; })} />
          </div>
          <TextField id={`${at}-pattern`} label={t('builder.pattern')} dir="ltr" value={field.pattern ?? ''} error={err(`${at}.pattern`)}
            onChange={v => update(f => { f.pattern = orNull(v); })} />
          <MultilingualField id={`${at}-pm`} optional label={t('builder.patternMsg')} value={field.patternMessage}
            onChange={v => update(f => { f.patternMessage = v; })} />
        </>
      )}

      {field.type === 'number' && (
        <div className="grid gap-x-4 md:grid-cols-2">
          <NumberField id={`${at}-min`} label={t('builder.min')} value={field.min} onChange={v => update(f => { f.min = v; })} />
          <NumberField id={`${at}-max`} label={t('builder.max')} value={field.max} error={err(`${at}.max`)} onChange={v => update(f => { f.max = v; })} />
        </div>
      )}
    </Panel>
  );
}

function OptionsEditor({ field, at, err, update }: {
  field: FieldDefinition; at: string; err: EditorProps['err']; update: (fn: (f: FieldDefinition) => void) => void;
}) {
  const options = field.options ?? [];
  return (
    <fieldset className="mb-5 rounded-lg border border-aeblack-100 p-4">
      <legend className="px-1 text-sm font-semibold text-aeblack-800">{t('builder.options')}</legend>
      {err(`${at}.options`) && <Notice kind="error">{err(`${at}.options`)}</Notice>}
      {options.map((o, i) => (
        <div key={i} className="mb-4 grid items-start gap-x-3 border-b border-aeblack-100 pb-2 md:grid-cols-[12rem_minmax(0,1fr)_auto]">
          <TextField id={`${at}-o${i}-v`} label={t('builder.optionValue')} dir="ltr" value={o.value} error={err(`${at}.options[${i}].value`)}
            onChange={v => update(f => { f.options![i].value = v; })} />
          <MultilingualField id={`${at}-o${i}-label`} label={t('builder.label')} value={o.label}
            error={code => err(`${at}.options[${i}].label.${code}`)} onChange={v => update(f => { f.options![i].label = v ?? {}; })} />
          <div className="pt-7">
            <AegovButton appearance="link" variant="danger" size="sm" onAegovButtonClick={() => update(f => { f.options!.splice(i, 1); })}>
              {t('builder.remove')}
            </AegovButton>
          </div>
        </div>
      ))}
      <AegovButton appearance="outline" size="sm" onAegovButtonClick={() => update(f => {
        const n = (f.options ?? []).length + 1;
        f.options = [...(f.options ?? []), { value: `option${n}`, label: textNow(t('builder.optionN', { n })) }];
      })}>{t('builder.addOption')}</AegovButton>
    </fieldset>
  );
}

/** The form exactly as citizens will see it, in the current language. Nothing is saved. */
export function PreviewSection({ def }: { def: ServiceDefinition }) {
  const [current, setCurrent] = useState(0);
  const [values, setValues] = useState<FormData>({});
  const index = Math.min(current, def.steps.length - 1);
  const step = def.steps[index];
  return (
    <>
      <Notice kind="info">{t('builder.previewIntro')}</Notice>
      <Panel title={loc(def.name, def.code)}>
        <p className="mb-5 text-aeblack-600">{loc(def.description)}</p>
        <StepProgress titles={def.steps.map(s => loc(s.title, s.id))} current={index} />
        {step && (
          <StepFields step={step} values={values} onChange={(k, v) => setValues(p => ({ ...p, [k]: v }))}
            onUpload={async file => ({ id: crypto.randomUUID(), fileName: file.name })} />
        )}
        <Actions align="between">
          {index > 0 ? <AegovButton appearance="outline" onAegovButtonClick={() => setCurrent(index - 1)}>{t('back')}</AegovButton> : <span />}
          {index < def.steps.length - 1 && <AegovButton onAegovButtonClick={() => setCurrent(index + 1)}>{t('form.saveContinue')}</AegovButton>}
        </Actions>
      </Panel>
  );
}

const versionStatus = { Draft: 'aegold', Published: 'aegreen', Retired: 'slate' } as const;

export function VersionsSection({ versions }: { versions: ServiceVersionInfo[] }) {
  const columns = useMemo<ColumnDef<ServiceVersionInfo, string>[]>(() => [
    { id: 'version', header: t('builder.version'), accessorFn: v => String(v.version).padStart(4, '0'), cell: c => <strong>v{c.row.original.version}</strong> },
    {
      id: 'status', header: t('status'), accessorFn: v => v.status,
      cell: c => <Tag variant={versionStatus[c.row.original.status]}>{t(`builder.vstatus.${c.row.original.status}`)}</Tag>,
      meta: { filter: true, filterLabel: v => t(`builder.vstatus.${v}`) },
    },
    { id: 'created', header: t('builder.created'), accessorFn: v => v.createdAt, cell: c => `${formatDate(c.getValue(), true)} · ${c.row.original.createdBy}` },
    {
      id: 'published', header: t('builder.publishedAt'), accessorFn: v => v.publishedAt ?? '',
      cell: c => (c.getValue() ? `${formatDate(c.getValue(), true)} · ${c.row.original.publishedBy ?? ''}` : '—'),
    },
    { id: 'updated', header: t('builder.lastChange'), accessorFn: v => v.updatedAt, cell: c => `${formatDate(c.getValue(), true)} · ${c.row.original.updatedBy}` },
  ], []);
  return (
    <DataTable label={t('builder.tab.versions')} data={versions} columns={columns} getRowId={v => String(v.version)}
      emptyTitle={t('builder.empty')} />
  );
}
