import { useMemo, useState } from 'react';
import {
  FIELD_TYPES, formatDate, loc, PROFILE_CLAIMS, t, type FieldDefinition, type FieldType, type FormData, type FormStep,
  type ServiceDefinition, type ServiceVersionInfo, type TKey,
} from '@dsp/core';
import {
  Actions, AegovButton, CheckboxField, ChoiceField, DataTable, moveItem, Notice, NumberField, Panel, ReorderList,
  StepFields, StepProgress, Tag, TextAreaField, TextField, type ColumnDef,
} from '@dsp/ui';

// The sections of the service builder. Every input is a shared @dsp/ui field (AEGOV underneath);
// the step and field lists use the shared reorderable list.

export interface EditorProps {
  def: ServiceDefinition;
  change: (updater: (d: ServiceDefinition) => void) => void;
  err: (path: string) => string | undefined;
}

const orNull = (v: string) => (v.trim() === '' ? null : v);

/** Two inputs side by side: the English and the Arabic version of one text. */
function Bilingual({ id, label, labelAr, en, ar, onEn, onAr, errEn, errAr, multiline }: {
  id: string; label: string; labelAr: string; en?: string | null; ar?: string | null;
  onEn: (v: string) => void; onAr: (v: string) => void; errEn?: string; errAr?: string; multiline?: boolean;
}) {
  const Field = multiline ? TextAreaField : TextField;
  return (
    <div className="grid gap-x-4 md:grid-cols-2">
      <Field id={`${id}-en`} label={label} dir="ltr" value={en ?? ''} onChange={onEn} error={errEn} />
      <Field id={`${id}-ar`} label={labelAr} dir="rtl" value={ar ?? ''} onChange={onAr} error={errAr} />
    </div>
  );
}

export function DetailsSection({ def, change, err }: EditorProps) {
  const textFields = def.steps.flatMap(s => s.fields).filter(f => f.type === 'text' || f.type === 'email' || f.type === 'phone');
  return (
    <Panel>
      <Bilingual id="name" label={t('builder.nameEn')} labelAr={t('builder.nameAr')} en={def.name} ar={def.nameAr}
        errEn={err('name')} errAr={err('nameAr')}
        onEn={v => change(d => { d.name = v; })} onAr={v => change(d => { d.nameAr = orNull(v); })} />
      <Bilingual id="desc" multiline label={t('builder.descEn')} labelAr={t('builder.descAr')} en={def.description} ar={def.descriptionAr}
        errEn={err('description')} errAr={err('descriptionAr')}
        onEn={v => change(d => { d.description = v; })} onAr={v => change(d => { d.descriptionAr = orNull(v); })} />
      <Bilingual id="cat" label={t('builder.categoryEn')} labelAr={t('builder.categoryAr')} en={def.category} ar={def.categoryAr}
        onEn={v => change(d => { d.category = v; })} onAr={v => change(d => { d.categoryAr = orNull(v); })} />
      <div className="grid gap-x-4 md:grid-cols-3">
        <NumberField id="fee" label={t('builder.fee')} value={def.fee} error={err('fee')} onChange={v => change(d => { d.fee = v ?? 0; })} />
        <TextField id="currency" label={t('builder.currency')} dir="ltr" value={def.currency} error={err('currency')}
          onChange={v => change(d => { d.currency = v.toUpperCase().slice(0, 3); })} />
        <ChoiceField id="referenceField" as="select" label={t('builder.referenceField')} hint={t('builder.referenceHelp')}
          error={err('referenceField')} value={def.referenceField ?? ''}
          choices={[{ value: '', label: t('builder.none') }, ...textFields.map(f => ({ value: f.key, label: `${loc(f.label, f.labelAr)} (${f.key})` }))]}
          onChange={v => change(d => { d.referenceField = v || null; })} />
      </div>
      <ChoiceField id="process" as="cards" label={t('builder.process')} error={err('workflowVersion')} value={String(def.workflowVersion)}
        choices={[
          { value: '1', label: t('process.v1'), hint: t('builder.processV1') },
          { value: '2', label: t('process.v2'), hint: t('builder.processV2') },
        ]}
        onChange={v => change(d => { d.workflowVersion = Number(v); })} />
    </Panel>
  );
}

export function ReviewSection({ def, change, err }: EditorProps) {
  const listError = err('reviewChecklist') ?? (def.reviewChecklist ?? []).map((_, i) => err(`reviewChecklist[${i}]`)).find(Boolean);
  return (
    <Panel>
      {def.workflowVersion !== 2 && <Notice kind="info">{t('builder.checklistV1')}</Notice>}
      <TextAreaField id="checklist" label={t('builder.checklist')} hint={t('builder.checklistHelp')} error={listError}
        value={(def.reviewChecklist ?? []).join('\n')}
        onChange={v => change(d => { d.reviewChecklist = v.split('\n').map(l => l.trim()).filter(Boolean); })} />
    </Panel>
  );
}

const newField = (): FieldDefinition => ({
  key: `field${Date.now() % 100000}`, label: 'New field', labelAr: 'حقل جديد', type: 'text', required: false,
});

/** Steps and fields: reorder by dragging, select one to edit its settings. */
export function FormSection({ def, change, err }: EditorProps) {
  const [stepIndex, setStepIndex] = useState(0);
  const [fieldIndex, setFieldIndex] = useState(0);
  const si = Math.min(stepIndex, def.steps.length - 1);
  const step = def.steps[si];
  const fi = Math.min(fieldIndex, Math.max(0, step.fields.length - 1));
  const field = step.fields[fi];
  const at = `steps[${si}]`;
  const fieldHasError = (i: number) => ['key', 'label', 'labelAr', 'type', 'options', 'pattern', 'prefillFrom', 'minLength', 'maxLength', 'max']
    .some(k => err(`${at}.fields[${i}].${k}`));

  const stepItems = def.steps.map((s, i) => ({
    id: `s${i}`, textValue: loc(s.title, s.titleAr),
    content: (
      <span className="flex items-center justify-between gap-2">
        <span>{i + 1}. {loc(s.title, s.titleAr)}</span>
        <Tag variant="slate">{s.fields.length}</Tag>
      </span>
    ),
  }));
  const fieldItems = step.fields.map((f, i) => ({
    id: `f${i}`, textValue: loc(f.label, f.labelAr),
    content: (
      <span className="flex flex-wrap items-center justify-between gap-2">
        <span>{loc(f.label, f.labelAr)}</span>
        <span className="flex gap-1">
          <Tag variant="slate">{t(`builder.type.${f.type}` as TKey)}</Tag>
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
              change(d => { d.steps.push({ id: `step-${n}`, title: `Step ${n}`, titleAr: `الخطوة ${n}`, fields: [newField()] }); });
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
      <Bilingual id={`${at}-title`} label={t('builder.titleEn')} labelAr={t('builder.titleAr')} en={step.title} ar={step.titleAr}
        errEn={err(`${at}.title`)} errAr={err(`${at}.titleAr`)}
        onEn={v => update(s => { s.title = v; })} onAr={v => update(s => { s.titleAr = orNull(v); })} />
      <Bilingual id={`${at}-desc`} multiline label={t('builder.stepDescEn')} labelAr={t('builder.stepDescAr')}
        en={step.description} ar={step.descriptionAr}
        onEn={v => update(s => { s.description = orNull(v); })} onAr={v => update(s => { s.descriptionAr = orNull(v); })} />
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
          choices={FIELD_TYPES.map(type => ({ value: type, label: t(`builder.type.${type}` as TKey) }))}
          onChange={v => update(f => {
            f.type = v as FieldType;
            if (f.type === 'select' && !f.options?.length) f.options = [{ value: 'option1', label: 'Option 1', labelAr: 'الخيار 1' }];
          })} />
      </div>
      <Bilingual id={`${at}-label`} label={t('builder.labelEn')} labelAr={t('builder.labelAr')} en={field.label} ar={field.labelAr}
        errEn={err(`${at}.label`)} errAr={err(`${at}.labelAr`)}
        onEn={v => update(f => { f.label = v; })} onAr={v => update(f => { f.labelAr = orNull(v); })} />
      <CheckboxField id={`${at}-required`} label={t('builder.required')} checked={field.required}
        onChange={c => update(f => { f.required = c; })} />
      <Bilingual id={`${at}-help`} label={t('builder.helpEn')} labelAr={t('builder.helpAr')} en={field.help} ar={field.helpAr}
        onEn={v => update(f => { f.help = orNull(v); })} onAr={v => update(f => { f.helpAr = orNull(v); })} />

      {field.type === 'select' && <OptionsEditor field={field} at={at} err={err} update={update} />}

      {isText && (
        <>
          <Bilingual id={`${at}-ph`} label={t('builder.placeholderEn')} labelAr={t('builder.placeholderAr')}
            en={field.placeholder} ar={field.placeholderAr}
            onEn={v => update(f => { f.placeholder = orNull(v); })} onAr={v => update(f => { f.placeholderAr = orNull(v); })} />
          <div className="grid gap-x-4 md:grid-cols-3">
            <NumberField id={`${at}-minl`} label={t('builder.minLength')} value={field.minLength} error={err(`${at}.minLength`)}
              onChange={v => update(f => { f.minLength = v; })} />
            <NumberField id={`${at}-maxl`} label={t('builder.maxLength')} value={field.maxLength} error={err(`${at}.maxLength`)}
              onChange={v => update(f => { f.maxLength = v; })} />
            <ChoiceField id={`${at}-prefill`} as="select" label={t('builder.prefill')} value={field.prefillFrom ?? ''}
              error={err(`${at}.prefillFrom`)}
              choices={[{ value: '', label: t('builder.none') }, ...PROFILE_CLAIMS.map(c => ({ value: c, label: t(`builder.prefill.${c}` as TKey) }))]}
              onChange={v => update(f => { f.prefillFrom = v || null; })} />
          </div>
          <TextField id={`${at}-pattern`} label={t('builder.pattern')} dir="ltr" value={field.pattern ?? ''} error={err(`${at}.pattern`)}
            onChange={v => update(f => { f.pattern = orNull(v); })} />
          <Bilingual id={`${at}-pm`} label={t('builder.patternMsgEn')} labelAr={t('builder.patternMsgAr')}
            en={field.patternMessage} ar={field.patternMessageAr}
            onEn={v => update(f => { f.patternMessage = orNull(v); })} onAr={v => update(f => { f.patternMessageAr = orNull(v); })} />
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
        <div key={i} className="grid items-start gap-x-3 md:grid-cols-[1fr_1.4fr_1.4fr_auto]">
          <TextField id={`${at}-o${i}-v`} label={t('builder.optionValue')} dir="ltr" value={o.value} error={err(`${at}.options[${i}].value`)}
            onChange={v => update(f => { f.options![i].value = v; })} />
          <TextField id={`${at}-o${i}-en`} label={t('builder.labelEn')} dir="ltr" value={o.label} error={err(`${at}.options[${i}].label`)}
            onChange={v => update(f => { f.options![i].label = v; })} />
          <TextField id={`${at}-o${i}-ar`} label={t('builder.labelAr')} dir="rtl" value={o.labelAr ?? ''} error={err(`${at}.options[${i}].labelAr`)}
            onChange={v => update(f => { f.options![i].labelAr = orNull(v); })} />
          <div className="pt-7">
            <AegovButton appearance="link" variant="danger" size="sm" onAegovButtonClick={() => update(f => { f.options!.splice(i, 1); })}>
              {t('builder.remove')}
            </AegovButton>
          </div>
        </div>
      ))}
      <AegovButton appearance="outline" size="sm" onAegovButtonClick={() => update(f => {
        const n = (f.options ?? []).length + 1;
        f.options = [...(f.options ?? []), { value: `option${n}`, label: `Option ${n}`, labelAr: `الخيار ${n}` }];
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
      <Panel title={loc(def.name, def.nameAr)}>
        <p className="mb-5 text-aeblack-600">{loc(def.description, def.descriptionAr)}</p>
        <StepProgress titles={def.steps.map(s => loc(s.title, s.titleAr))} current={index} />
        {step && (
          <StepFields step={step} values={values} onChange={(k, v) => setValues(p => ({ ...p, [k]: v }))}
            onUpload={async file => ({ id: crypto.randomUUID(), fileName: file.name })} />
        )}
        <Actions align="between">
          {index > 0 ? <AegovButton appearance="outline" onAegovButtonClick={() => setCurrent(index - 1)}>{t('back')}</AegovButton> : <span />}
          {index < def.steps.length - 1 && <AegovButton onAegovButtonClick={() => setCurrent(index + 1)}>{t('form.saveContinue')}</AegovButton>}
        </Actions>
      </Panel>
    </>
  );
}

const versionStatus = { Draft: 'aegold', Published: 'aegreen', Retired: 'slate' } as const;

export function VersionsSection({ versions }: { versions: ServiceVersionInfo[] }) {
  const columns = useMemo<ColumnDef<ServiceVersionInfo, string>[]>(() => [
    { id: 'version', header: t('builder.version'), accessorFn: v => String(v.version).padStart(4, '0'), cell: c => <strong>v{c.row.original.version}</strong> },
    {
      id: 'status', header: t('status'), accessorFn: v => v.status,
      cell: c => <Tag variant={versionStatus[c.row.original.status]}>{t(`builder.vstatus.${c.row.original.status}` as TKey)}</Tag>,
      meta: { filter: true, filterLabel: v => t(`builder.vstatus.${v}` as TKey) },
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
