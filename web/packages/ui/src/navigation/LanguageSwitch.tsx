import { AegovButton, AegovDropdown } from '@aegov/designsystem-react';
import { getLang, getLanguages, loc, setLang, t, type LanguageInfo } from '@dsp/core';
import { Globe, Icon } from '../icons';

/** A language in its own name (e.g. "العربية"), as language switches show them. */
export function ownName(language: LanguageInfo): string {
  return language.name[language.code] || loc(language.name, language.code);
}

/**
 * Switches language. Languages are data: with one or two other enabled languages they are links in the top bar,
 * each in its own name; with more, a menu. The layout flips when the direction changes.
 */
export function LanguageSwitch({ onDark = false }: {
  /** In the header's dark top bar: light text (neutral-min) so it stays readable. */
  onDark?: boolean;
}) {
  const variant = onDark ? 'neutral-min' : 'neutral-max';
  const others = getLanguages().filter(l => l.code !== getLang());
  if (others.length === 0) return null;

  const button = (l: LanguageInfo) => (
    <span key={l.code} lang={l.code} dir={l.direction}>
      <AegovButton appearance="link" variant={variant} size="sm"
        aria-label={t('lang.switchTo', { language: ownName(l) })}
        onAegovButtonClick={() => { void setLang(l.code); }}>
        <span className="inline-flex items-center gap-2"><Icon icon={Globe} size="size-5" />{ownName(l)}</span>
      </AegovButton>
    </span>
  );

  if (others.length <= 2) return <span className="inline-flex items-center gap-2">{others.map(button)}</span>;

  // The dropdown moves its content to <body>, so only AEGOV events are used inside it.
  return (
    <AegovDropdown>
      <AegovButton appearance="link" variant={variant} size="sm">
        <span className="inline-flex items-center gap-2"><Icon icon={Globe} size="size-5" />{t('lang.choose')}</span>
      </AegovButton>
      <div slot="dropdown-content" className="flex w-48 flex-col items-start gap-1 p-2">{others.map(l => (
        <span key={l.code} lang={l.code} dir={l.direction}>
          <AegovButton appearance="link" variant="neutral-max" size="sm"
            aria-label={t('lang.switchTo', { language: ownName(l) })}
            onAegovButtonClick={() => { void setLang(l.code); }}>
            {ownName(l)}
          </AegovButton>
        </span>
      ))}</div>
    </AegovDropdown>
  );
}
