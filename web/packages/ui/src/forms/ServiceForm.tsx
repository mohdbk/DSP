import type { ReactNode } from 'react';
import { AegovButton, AegovTimeline, AegovTimelineStep } from '@aegov/designsystem-react';
import { loc, t, type FieldDefinition, type FieldError, type FormData, type FormStep } from '@dsp/core';
import { CheckboxField, ChoiceField, DateField, FileField, TextAreaField, TextField } from './fields';
import { DescriptionList } from '../data/DescriptionList';

// An e-service form rendered from its definition. The citizen form view, the officer's review, the
// status page and the service builder's preview all render services through these components.

export interface UploadHandler { (file: File): Promise<{ id: string; fileName: string }> }

/** One field of a service definition, as the matching AEGOV control. */
export function ServiceField({ field, value, error, onChange, onUpload, fileName, onFileName }: {
  field: FieldDefinition;
  value: string;
  error?: string;
  onChange: (value: string) => void;
  onUpload?: UploadHandler;
  fileName?: string;
  onFileName?: (name: string) => void;
}) {
  const common = {
    id: `f-${field.key}`,
    label: loc(field.label),
    hint: loc(field.help),
    error,
    required: field.required,
    markOptional: true,
    dir: field.key.endsWith('Ar') ? 'rtl' as const : field.key.endsWith('En') ? 'ltr' as const : undefined,
  };

  switch (field.type) {
    case 'checkbox':
      return <CheckboxField {...common} markOptional={false} checked={value === 'true'} onChange={c => onChange(c ? 'true' : '')} />;
    case 'select':
      return <ChoiceField {...common} value={value} onChange={onChange}
        choices={(field.options ?? []).map(o => ({ value: o.value, label: loc(o.label) }))} />;
    case 'date':
      return <DateField {...common} value={value} onChange={onChange} />;
    case 'textarea':
      return <TextAreaField {...common} value={value} onChange={onChange} maxLength={field.maxLength}
        placeholder={loc(field.placeholder)} />;
    case 'file':
      return <FileField {...common} value={value} fileName={fileName}
        onUpload={onUpload ?? (async () => { throw new Error(t('form.uploadFailed')); })}
        onChange={(id, name) => { onFileName?.(name); onChange(id); }} />;
    default:
      return <TextField {...common} value={value} onChange={onChange}
        type={field.type === 'email' ? 'email' : field.type === 'phone' ? 'tel' : field.type === 'number' ? 'number' : 'text'}
        placeholder={loc(field.placeholder)} />;
  }
}

/** Every field of one step, editable. */
export function StepFields({ step, values, errors = [], onChange, onUpload, fileNames = {}, onFileName }: {
  step: FormStep;
  values: FormData;
  errors?: FieldError[];
  onChange: (key: string, value: string) => void;
  onUpload?: UploadHandler;
  fileNames?: Record<string, string>;
  onFileName?: (key: string, name: string) => void;
}) {
  return (
    <div>
      {step.description && <p className="mb-5 text-aeblack-600">{loc(step.description)}</p>}
      {step.fields.map(field => (
        <ServiceField key={field.key} field={field} value={values[field.key] ?? ''}
          error={errors.find(e => e.field.toLowerCase() === field.key.toLowerCase())?.message}
          onChange={v => onChange(field.key, v)} onUpload={onUpload}
          fileName={fileNames[field.key]} onFileName={name => onFileName?.(field.key, name)} />
      ))}
    </div>
  );
}

/** How a stored answer reads (option labels, confirmations, files). */
export function displayValue(field: FieldDefinition, value: string | null | undefined, fileName?: string,
  onOpenFile?: (documentId: string) => void): ReactNode {
  if (!value) return <span className="text-aeblack-500">{t('form.notProvided')}</span>;
  if (field.type === 'checkbox') return value === 'true' ? t('form.confirmed') : t('form.notConfirmed');
  if (field.type === 'select') {
    const option = field.options?.find(o => o.value === value);
    return option ? loc(option.label) : value;
  }
  if (field.type === 'file') {
    return onOpenFile
      ? <AegovButton appearance="link" size="sm" onAegovButtonClick={() => onOpenFile(value)}>{fileName ?? t('form.openFile')}</AegovButton>
      : fileName ?? t('form.attached');
  }
  return <bdi>{value}</bdi>;
}

/** The answers to one step, read-only. */
export function StepAnswers({ step, values, fileNames = {}, onOpenFile }: {
  step: FormStep;
  values: FormData;
  fileNames?: Record<string, string>;
  onOpenFile?: (documentId: string) => void;
}) {
  return (
    <DescriptionList items={step.fields.map(f => ({
      label: loc(f.label),
      value: displayValue(f, values[f.key], fileNames[f.key], onOpenFile),
    }))} />
  );
}

/** All answers of an application, step by step, with an optional action per step (e.g. "Change"). */
export function ServiceAnswers({ steps, values, fileNames, onOpenFile, stepAction }: {
  steps: FormStep[];
  values: FormData;
  fileNames?: Record<string, string>;
  onOpenFile?: (documentId: string) => void;
  stepAction?: (index: number) => ReactNode;
}) {
  return (
    <div className="flex flex-col gap-6">
      {steps.map((s, i) => (
        <section key={s.id}>
          <div className="flex items-center justify-between gap-3">
            <h3 className="text-h6 font-bold text-aeblack-900">{loc(s.title)}</h3>
            {stepAction?.(i)}
          </div>
          <StepAnswers step={s} values={values} fileNames={fileNames} onOpenFile={onOpenFile} />
        </section>
      ))}
    </div>
  );
}

/** Where the user is in a multi-step form (AEGOV timeline; the design system has no stepper). */
export function StepProgress({ titles, current }: { titles: string[]; current: number }) {
  return (
    <div className="mb-6 overflow-x-auto">
      <AegovTimeline key={titles.length}>
        {titles.map((title, i) => (
          <AegovTimelineStep key={i} status={i < current ? 'completed' : i === current ? 'active' : 'pending'}>
            {title}
          </AegovTimelineStep>
        ))}
      </AegovTimeline>
    </div>
  );
}
