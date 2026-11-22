import { getDefaultLang, getLanguages, t, type LanguageInfo, type LocalizedText } from '@dsp/core';
import { TextAreaField, TextField } from './fields';
import { ownName } from '../navigation/LanguageSwitch';

/**
 * One text in every enabled language: an input per language, the default language first and required, each in its
 * own direction. Languages are data, so the inputs follow whatever languages the platform has enabled.
 */
export function MultilingualField({ id, label, value, onChange, error, multiline, hint, optional }: {
  id: string;
  label: string;
  value: LocalizedText | null | undefined;
  onChange: (value: LocalizedText | null) => void;
  /** The error for one language (paths like "name.en"). */
  error?: (language: string) => string | undefined;
  multiline?: boolean;
  hint?: string;
  /** The text isn't required even in the default language. */
  optional?: boolean;
}) {
  const Field = multiline ? TextAreaField : TextField;
  const languages = orderedLanguages();
  const set = (code: string, text: string) => {
    const next: LocalizedText = { ...(value ?? {}) };
    if (text.trim() === '') delete next[code]; else next[code] = text;
    onChange(Object.keys(next).length ? next : null);
  };
  return (
    <div className={languages.length > 1 ? 'grid gap-x-4 md:grid-cols-2' : undefined}>
      {languages.map(l => (
        <Field key={l.code} id={`${id}-${l.code}`} lang={l.code} dir={l.direction}
          label={languages.length > 1 ? t('builder.inLanguage', { label, language: ownName(l) }) : label}
          required={!optional && l.isDefault} markOptional={!l.isDefault || optional}
          hint={l.isDefault ? hint : undefined}
          value={value?.[l.code] ?? ''} error={error?.(l.code)} onChange={v => set(l.code, v)} />
      ))}
    </div>
  );
}

/** The enabled languages with the default first. */
export function orderedLanguages(): LanguageInfo[] {
  const all = getLanguages();
  const dflt = getDefaultLang();
  return [...all.filter(l => l.code === dflt), ...all.filter(l => l.code !== dflt)];
}
