import { createSession } from '@dsp/core';

export interface StaffUser { id: string; name: string; title?: string; email?: string }

export const { api, currentUser, useCurrentUser, signOut } = createSession<StaffUser>({
  storageKey: 'dsp.backoffice.token',
  role: 'staff',
  loginPath: '/login',
  mapUser: c => ({ id: c.sub, name: c.name, title: c.title, email: c.email }),
  signOutUrl: () => '/login',
});
