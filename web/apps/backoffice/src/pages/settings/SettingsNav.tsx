import { t } from '@dsp/core';
import { AegovLink } from '@dsp/ui';

/** Links between the settings pages (languages, texts, AI). */
export function SettingsNav({ current }: { current: 'languages' | 'translations' | 'ai' }) {
  const pages = [
    ['languages', '/settings/languages', 'settings.languages'],
    ['translations', '/settings/translations', 'settings.translations'],
    ['ai', '/settings/ai', 'settings.ai'],
  ] as const;
  return (
    <nav aria-label={t('settings.title')} className="flex flex-wrap gap-4">
      {pages.filter(([id]) => id !== current).map(([id, href, key]) => <AegovLink key={id} href={href}>{t(key)}</AegovLink>)}
    </nav>
  );
}
