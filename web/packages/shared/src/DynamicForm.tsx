import { useState } from 'react';
import type { FieldDefinition, FieldError, FormData, FormStep } from './types';
import { loc, t } from './i18n';

export interface DynamicFormProps {
  step: FormStep;
  values: FormData;
  errors?: FieldError[];
  readOnly?: boolean;
  onChange?: (key: string, value: string) => void;
  /** Uploads a file and returns its document ID. */
  onUpload?: (field: FieldDefinition, file: File) => Promise<string>;
  /** Opens an uploaded document (read-only mode). */
  onOpenFile?: (documentId: string) => void;
  fileNames?: Record<string, string>;
}

/** Renders one workflow step from its schema. Used by the citizen form view and back-office review. */
export function DynamicForm({ step, values, errors = [], readOnly, onChange, onUpload, onOpenFile, fileNames = {} }: DynamicFormProps) {
  return (
    <div className="form-step">
      {step.description && <p className="muted">{loc(step.description, step.descriptionAr)}</p>}
      {step.fields.map(field => (
        <Field
          key={field.key}
          field={field}
          value={values[field.key] ?? ''}
          error={errors.find(e => e.field.toLowerCase() === field.key.toLowerCase())?.message}
          readOnly={readOnly}
          onChange={v => onChange?.(field.key, v)}
          onUpload={onUpload}
          onOpenFile={onOpenFile}
          fileName={fileNames[field.key]}
        />
      ))}
    </div>
  );
}

interface FieldProps {
  field: FieldDefinition;
  value: string;
  error?: string;
  readOnly?: boolean;
  onChange: (value: string) => void;
  onUpload?: DynamicFormProps['onUpload'];
  onOpenFile?: DynamicFormProps['onOpenFile'];
  fileName?: string;
}

function Field({ field, value, error, readOnly, onChange, onUpload, onOpenFile, fileName }: FieldProps) {
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string>();
  const id = `f-${field.key}`;
  const describedBy = error || uploadError ? `${id}-error` : field.help ? `${id}-help` : undefined;
  const label = loc(field.label, field.labelAr);
  const help = loc(field.help, field.helpAr);
  const placeholder = loc(field.placeholder, field.placeholderAr) ?? '';

  if (readOnly) {
    return (
      <div className="field field-readonly">
        <div className="label">{label}</div>
        <div className="value">{displayValue(field, value, fileName, onOpenFile)}</div>
      </div>
    );
  }

  if (field.type === 'checkbox') {
    return (
      <div className={`field field-check ${error ? 'has-error' : ''}`}>
        <label htmlFor={id}>
          <input id={id} type="checkbox" checked={value === 'true'} aria-describedby={describedBy}
            onChange={e => onChange(e.target.checked ? 'true' : '')} />
          <span>{label}{field.required && <span className="req"> *</span>}</span>
        </label>
        {error && <div id={`${id}-error`} className="error-text">{error}</div>}
      </div>
    );
  }

  const common = {
    id,
    value,
    'aria-invalid': !!error,
    'aria-describedby': describedBy,
    onChange: (e: { target: { value: string } }) => onChange(e.target.value),
  };

  let input;
  switch (field.type) {
    case 'textarea':
      input = <textarea {...common} rows={4} maxLength={field.maxLength ?? undefined} placeholder={placeholder} />;
      break;
    case 'select':
      input = (
        <select {...common}>
          <option value="">{t('form.select')}</option>
          {field.options?.map(o => <option key={o.value} value={o.value}>{loc(o.label, o.labelAr)}</option>)}
        </select>
      );
      break;
    case 'date':
      input = <input {...common} type="date" />;
      break;
    case 'number':
      input = <input {...common} type="number" min={field.min ?? undefined} max={field.max ?? undefined} />;
      break;
    case 'file':
      input = (
        <div className="file-input">
          <input id={id} type="file" accept="application/pdf,image/png,image/jpeg" disabled={uploading}
            aria-describedby={describedBy}
            onChange={async e => {
              const file = e.target.files?.[0];
              if (!file || !onUpload) return;
              setUploading(true);
              setUploadError(undefined);
              try {
                onChange(await onUpload(field, file));
              } catch (err) {
                setUploadError(err instanceof Error ? err.message : t('form.uploadFailed'));
              } finally {
                setUploading(false);
              }
            }} />
          {uploading && <span className="muted">{t('form.uploading')}</span>}
          {!uploading && value && <span className="ok-text">✓ {fileName ?? t('form.uploaded')}</span>}
        </div>
      );
      break;
    default:
      input = <input {...common} type={field.type === 'email' ? 'email' : field.type === 'phone' ? 'tel' : 'text'}
        maxLength={field.maxLength ?? undefined} placeholder={placeholder}
        dir={field.key.endsWith('Ar') ? 'rtl' : field.key.endsWith('En') || field.type === 'email' || field.type === 'phone' ? 'ltr' : undefined} />;
  }

  return (
    <div className={`field ${error || uploadError ? 'has-error' : ''}`}>
      <label htmlFor={id}>{label}{field.required && <span className="req"> *</span>}</label>
      {input}
      {help && !error && <div id={`${id}-help`} className="help-text">{help}</div>}
      {(error || uploadError) && <div id={`${id}-error`} className="error-text">{error || uploadError}</div>}
    </div>
  );
}

function displayValue(field: FieldDefinition, value: string, fileName?: string, onOpenFile?: (id: string) => void) {
  if (!value) return <span className="muted">{t('form.notProvided')}</span>;
  if (field.type === 'checkbox') return value === 'true' ? t('form.confirmed') : t('form.notConfirmed');
  if (field.type === 'select') {
    const option = field.options?.find(o => o.value === value);
    return option ? loc(option.label, option.labelAr) : value;
  }
  if (field.type === 'file') {
    return onOpenFile
      ? <button type="button" className="link" onClick={() => onOpenFile(value)}>{fileName ?? t('form.openFile')}</button>
      : fileName ?? t('form.attached');
  }
  return <bdi>{value}</bdi>;
}

/** Step indicator for multi-step forms. */
export function Stepper({ steps, current, onSelect }: { steps: FormStep[]; current: number; onSelect?: (i: number) => void }) {
  return (
    <ol className="stepper">
      {steps.map((s, i) => (
        <li key={s.id} className={i === current ? 'current' : i < current ? 'done' : ''}>
          <button type="button" onClick={() => onSelect?.(i)} disabled={!onSelect}>
            <span className="num">{i < current ? '✓' : i + 1}</span>
            <span className="title">{loc(s.title, s.titleAr)}</span>
          </button>
        </li>
      ))}
    </ol>
  );
}
