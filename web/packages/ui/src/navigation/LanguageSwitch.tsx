import { AegovButton } from '@aegov/designsystem-react';
import { getLang, setLang, t } from '@dsp/core';

/** Switches between English and Arabic; the whole layout flips to right-to-left. */
export function LanguageSwitch() {
  const next = getLang() === 'ar' ? 'en' : 'ar';
  return (
    <span lang={next}>
      <AegovButton appearance="link" variant="neutral-max" size="sm" aria-label={t('lang.switchLabel')}
        onAegovButtonClick={() => setLang(next)}>
        {t('lang.switch')}
      </AegovButton>
    </span>
  );
}
