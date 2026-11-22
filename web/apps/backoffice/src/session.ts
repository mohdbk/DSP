import { createApi, readClaims } from '@dsp/shared';

export const api = createApi('dsp.backoffice.token', () => {
  if (!location.pathname.startsWith('/login')) location.assign('/login');
});

export interface StaffUser { id: string; name: string; title?: string; email?: string }

export function currentUser(): StaffUser | null {
  const claims = readClaims(api.token());
  if (!claims || claims.role !== 'staff') return null;
  return { id: claims.sub, name: claims.name, title: claims.title, email: claims.email };
}

export function signOut() {
  api.setToken(null);
  location.assign('/login');
}
