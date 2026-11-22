import { createSession } from '@dsp/core';

export interface CitizenUser {
  id: string;
  name: string;
  emiratesId?: string;
  email?: string;
  provider?: string;
  accountLevel?: string;
}

export const { api, currentUser, useCurrentUser, signOut } = createSession<CitizenUser>({
  storageKey: 'dsp.portal.token',
  role: 'citizen',
  loginPath: '/login',
  mapUser: c => ({
    id: c.sub, name: c.name, emiratesId: c.emirates_id, email: c.email, provider: c.idp, accountLevel: c.account_level,
  }),
  signOutUrl: user => `/api/v1/identity/citizen/${user?.provider ?? 'uaepass'}/logout`,
});
