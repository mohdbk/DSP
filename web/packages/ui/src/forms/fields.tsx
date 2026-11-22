import { useState, type ReactNode } from 'react';
import {
  AegovCheckbox, AegovDatepicker, AegovFileupload, AegovFormField, AegovInput, AegovLabel, AegovRadio,
  AegovRadioGroup, AegovSelect, AegovSelectOption, AegovTextarea,
} from '@aegov/designsystem-react';
import { getLang, t } from '@dsp/core';

// The form fields of both portals — the dynamic e-service form, the service builder and sign-in all use these.
// Each wraps the AEGOV control with its label, hint and error, so every field behaves and looks the same.

interface BaseProps {
  id: string;
  label: string;
  hint?: string | null;
  error?: string | null;
  required?: boolean;
  /** Adds "(optional)" to the label of fields that aren't required (design system rule: no asterisks). */
  markOptional?: boolean;
  /** Text direction of the value, e.g. "rtl" for an Arabic-only field. */
  dir?: 'ltr' | 'rtl';
  /** Language of the value (multilingual inputs), so screen readers and fonts follow it. */
  lang?: string;
  disabled?: boolean;
}

function labelText({ label, required, markOptional }: BaseProps) {
  return markOptional && !required ? `${label} ${t('form.optional')}` : label;
}

function Framed({ props, children }: { props: BaseProps; children: ReactNode }) {
  return (
    <div className="mb-5" dir={props.dir} lang={props.lang}>
      <AegovFormField>
        <AegovLabel>{labelText(props)}</AegovLabel>
        {children}
      </AegovFormField>
    </div>
  );
}

export function TextField(props: BaseProps & {
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'email' | 'tel' | 'number' | 'password' | 'search';
  placeholder?: string | null;
}) {
  return (
    <Framed props={props}>
      <AegovInput fieldId={props.id} name={props.id} type={props.type ?? 'text'} value={props.value}
        placeholder={props.placeholder ?? undefined} isRequired={!!props.required} isDisabled={!!props.disabled}
        isInvalid={!!props.error} errorMessage={props.error ?? ''} hint={props.hint ?? ''}
        isRevealable={props.type === 'password'}
        onAegovInputValue={e => props.onChange(e.detail.value)} />
    </Framed>
  );
}

export function NumberField(props: BaseProps & { value: number | null | undefined; onChange: (value: number | null) => void }) {
  return (
    <TextField {...props} type="number" value={props.value == null ? '' : String(props.value)}
      onChange={v => props.onChange(v.trim() === '' || Number.isNaN(Number(v)) ? null : Number(v))} />
  );
}

export function TextAreaField(props: BaseProps & {
  value: string;
  onChange: (value: string) => void;
  maxLength?: number | null;
  placeholder?: string | null;
}) {
  return (
    <Framed props={props}>
      <AegovTextarea fieldId={props.id} name={props.id} value={props.value} maxlength={props.maxLength ?? undefined}
        placeholder={props.placeholder ?? undefined} isRequired={!!props.required} isDisabled={!!props.disabled}
        isInvalid={!!props.error} errorMessage={props.error ?? ''} hint={props.hint ?? ''}
        onAegovTextareaValue={e => props.onChange(e.detail.value)} />
    </Framed>
  );
}

export interface Choice { value: string; label: string; hint?: string }

/** One choice from a list. Five or fewer become radio buttons and more become a select, as the design system advises. */
export function ChoiceField(props: BaseProps & {
  value: string;
  onChange: (value: string) => void;
  choices: Choice[];
  /** Force a presentation; by default it follows the five-option rule. */
  as?: 'radio' | 'cards' | 'select';
  placeholder?: string;
}) {
  const as = props.as ?? (props.choices.length <= 5 ? 'radio' : 'select');
  if (as === 'select') {
    return (
      <Framed props={props}>
        <AegovSelect key={props.choices.map(c => c.value).join('|')} fieldId={props.id} name={props.id} value={props.value} placeholder={props.placeholder ?? t('form.select')}
          isRequired={!!props.required} isDisabled={!!props.disabled} isInvalid={!!props.error}
          errorMessage={props.error ?? ''} hint={props.hint ?? ''}
          onAegovSelectChange={e => props.onChange(String(e.detail.value ?? ''))}>
          {props.choices.map(c => <AegovSelectOption key={c.value} value={c.value}>{c.label}</AegovSelectOption>)}
        </AegovSelect>
      </Framed>
    );
  }
  return (
    <div className="mb-5" dir={props.dir}>
      <AegovRadioGroup key={props.choices.map(c => c.value).join('|')} name={props.id} label={labelText(props)} appearance={as === 'cards' ? 'cards' : 'default'}
        isRequired={!!props.required} isDisabled={!!props.disabled} isInvalid={!!props.error}
        errorMessage={props.error ?? ''} hint={props.hint ?? ''}
        onAegovRadioGroupChange={e => props.onChange(e.detail.value ?? '')}>
        {props.choices.map(c => (
          <AegovRadio key={c.value} fieldId={`${props.id}-${c.value}`} value={c.value} label={c.label}
            hint={c.hint} isChecked={props.value === c.value} />
        ))}
      </AegovRadioGroup>
    </div>
  );
}

/** Several choices from a list (AEGOV listbox select with multiple selection). */
export function MultiChoiceField(props: BaseProps & {
  value: string[];
  onChange: (value: string[]) => void;
  choices: Choice[];
  placeholder?: string;
}) {
  return (
    <Framed props={props}>
      <AegovSelect key={props.choices.map(c => c.value).join('|')} fieldId={props.id} name={props.id} isMultiple appearance="listbox"
        value={props.value.join(',')} placeholder={props.placeholder ?? t('form.select')} isClearable
        isRequired={!!props.required} isDisabled={!!props.disabled} isInvalid={!!props.error}
        errorMessage={props.error ?? ''} hint={props.hint ?? ''}
        onAegovSelectChange={e => {
          const v = e.detail.value as unknown;
          props.onChange((Array.isArray(v) ? v.map(String) : String(v ?? '').split(',')).filter(Boolean));
        }}>
        {props.choices.map(c => <AegovSelectOption key={c.value} value={c.value}>{c.label}</AegovSelectOption>)}
      </AegovSelect>
    </Framed>
  );
}

export function CheckboxField(props: BaseProps & { checked: boolean; onChange: (checked: boolean) => void }) {
  return (
    <div className="mb-5" dir={props.dir}>
      <AegovCheckbox fieldId={props.id} name={props.id} label={props.label} isChecked={props.checked}
        isRequired={!!props.required} isDisabled={!!props.disabled} isInvalid={!!props.error}
        errorMessage={props.error ?? ''} hint={props.hint ?? undefined}
        onAegovCheckboxChange={e => props.onChange(e.detail.checked)} />
    </div>
  );
}

/** A date as "YYYY-MM-DD" (AEGOV datepicker, in the UI language). */
export function DateField(props: BaseProps & { value: string; onChange: (value: string) => void }) {
  return (
    <Framed props={props}>
      <AegovDatepicker fieldId={props.id} name={props.id} value={props.value} format="YYYY-MM-DD"
        locale={getLang() === 'ar' ? 'ar-AE' : 'en'} isRequired={!!props.required} isDisabled={!!props.disabled}
        isInvalid={!!props.error} errorMessage={props.error ?? ''} hint={props.hint ?? ''}
        onAegovDatepickerChange={e => props.onChange(e.detail.value ?? '')} />
    </Framed>
  );
}

/**
 * A document upload (AEGOV file upload). The file is uploaded as soon as it is chosen and the field's value
 * becomes the stored document's ID.
 */
export function FileField(props: BaseProps & {
  value: string;
  fileName?: string;
  onUpload: (file: File) => Promise<{ id: string; fileName: string }>;
  onChange: (documentId: string, fileName: string) => void;
  /** Accepted file types (default: PDF and images). */
  accept?: string;
}) {
  const [busy, setBusy] = useState(false);
  const [uploadError, setUploadError] = useState<string>();
  const status = busy ? t('form.uploading') : props.value ? t('form.uploadedName', { name: props.fileName ?? t('form.uploaded') }) : undefined;
  const error = uploadError ?? props.error ?? '';

  return (
    <div className="mb-5">
      <AegovFileupload fieldId={props.id} name={props.id} label={labelText(props)} appearance="dropzone"
        accept={props.accept ?? '.pdf,.jpg,.jpeg,.png'} maxFileSize={10 * 1024 * 1024} isRequired={!!props.required && !props.value}
        isDisabled={busy || !!props.disabled} isInvalid={!!error} errorMessage={error}
        hint={[props.hint, status ?? t('form.fileHint')].filter(Boolean).join(' · ')}
        onAegovFileuploadChange={async e => {
          const file = e.detail.files[0];
          if (!file) return;
          setBusy(true);
          setUploadError(undefined);
          try {
            const doc = await props.onUpload(file);
            props.onChange(doc.id, doc.fileName);
          } catch (err) {
            setUploadError(err instanceof Error ? err.message : t('form.uploadFailed'));
          } finally {
            setBusy(false);
          }
        }} />
    </div>
  );
}
