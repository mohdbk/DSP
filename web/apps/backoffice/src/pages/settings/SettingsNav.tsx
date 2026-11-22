import { t } from '@dsp/core';
import { AegovLink, Coins, Globe, IconLabel, Robot, Translate } from '@dsp/ui';

/** Links between the settings pages (languages, texts, AI). */
export function SettingsNav({ current }: { current: 'languages' | 'translations' | 'currencies' | 'ai' }) {
  const pages = [
    ['languages', '/settings/languages', 'settings.languages', Globe],
    ['translations', '/settings/translations', 'settings.translations', Translate],
    ['currencies', '/settings/currencies', 'settings.currencies', Coins],
    ['ai', '/settings/ai', 'settings.ai', Robot],
  ] as const;
  return (
    <nav aria-label={t('settings.title')} className="flex flex-wrap gap-4">
      {pages.filter(([id]) => id !== current).map(([id, href, key, icon]) => (
        <AegovLink key={id} href={href}><IconLabel icon={icon}>{t(key)}</IconLabel></AegovLink>
      ))}
    </nav>
  );
}
