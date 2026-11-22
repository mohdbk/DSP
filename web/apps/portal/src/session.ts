import { createApi, readClaims } from '@dsp/shared';

export const api = createApi('dsp.portal.token', () => {
  if (!location.pathname.startsWith('/login')) location.assign(`/login?returnUrl=${encodeURIComponent(location.pathname)}`);
});

export interface CitizenUser {
  id: string;
  name: string;
  emiratesId?: string;
  email?: string;
  provider?: string;
  accountLevel?: string;
}

export function currentUser(): CitizenUser | null {
  const claims = readClaims(api.token());
  if (!claims || claims.role !== 'citizen') return null;
  return {
    id: claims.sub,
    name: claims.name,
    emiratesId: claims.emirates_id,
    email: claims.email,
    provider: claims.idp,
    accountLevel: claims.account_level,
  };
}

export function signOut() {
  const provider = currentUser()?.provider ?? 'uaepass';
  api.setToken(null);
  location.assign(`/api/identity/citizen/${provider}/logout`);
}
