import { AegovBadge, AegovButton, AegovLink } from '@aegov/designsystem-react';
import { t } from '@dsp/core';
import { Bell, Icon, SignOut } from '../icons';

/** The signed-in user in the header: notifications, name and role, and sign out. Both portals use it. */
export function AccountArea({ name, role, unread, notificationsHref, onSignOut }: {
  name: string;
  role: string;
  unread: number;
  /** Where the notifications link goes; without it the count is shown without a link. */
  notificationsHref?: string;
  onSignOut: () => void;
}) {
  const count = unread > 0 ? <AegovBadge variant="aered" size="sm">{unread}</AegovBadge> : null;
  return (
    <>
      {notificationsHref
        ? (
          <AegovLink href={notificationsHref} aria-label={t('notifications.unread', { n: unread })}>
            <span className="inline-flex items-center gap-2"><Icon icon={Bell} />{t('notifications')} {count}</span>
          </AegovLink>
        )
        : <span className="inline-flex items-center gap-2 text-sm" title={t('notifications.unread', { n: unread })}><Icon icon={Bell} size="size-5" />{t('notifications')} {count}</span>}
      <span className="text-end text-sm leading-tight">
        <span className="block font-semibold text-aeblack-900">{name}</span>
        <span className="block text-aeblack-600">{role}</span>
      </span>
      <AegovButton appearance="outline" variant="neutral-max" size="sm" onAegovButtonClick={onSignOut}>
        <span className="inline-flex items-center gap-2"><Icon icon={SignOut} size="size-5" mirror />{t('signOut')}</span>
      </AegovButton>
    </>
  );
}
