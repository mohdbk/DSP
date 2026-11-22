import { useMemo, useSyncExternalStore } from 'react';
import { createApi, readClaims, SESSION_EVENT, type ApiClient } from './api';

/** A signed-in user as the portals see them: the platform token's claims. */
export type Claims = Record<string, string>;

export interface Session<TUser> {
  api: ApiClient;
  currentUser(): TUser | null;
  /** The signed-in user as React state: components re-render when someone signs in or out. */
  useCurrentUser(): TUser | null;
  signOut(): void;
}

/**
 * One portal's session: where its token is kept, which role may use it, how claims map to its user,
 * and where signing out goes. Both portals build theirs from this.
 */
export function createSession<TUser>(options: {
  storageKey: string;
  role: string;
  loginPath: string;
  mapUser: (claims: Claims) => TUser;
  signOutUrl: (user: TUser | null) => string;
}): Session<TUser> {
  const api = createApi(options.storageKey, () => {
    if (!location.pathname.startsWith(options.loginPath)) {
      location.assign(`${options.loginPath}?returnUrl=${encodeURIComponent(location.pathname)}`);
    }
  });

  function currentUser(): TUser | null {
    const claims = readClaims(api.token());
    return claims && claims.role === options.role ? options.mapUser(claims) : null;
  }

  const subscribe = (notify: () => void) => {
    window.addEventListener(SESSION_EVENT, notify);
    window.addEventListener('storage', notify);
    return () => { window.removeEventListener(SESSION_EVENT, notify); window.removeEventListener('storage', notify); };
  };

  function useCurrentUser(): TUser | null {
    const token = useSyncExternalStore(subscribe, api.token, () => null);
    return useMemo(() => currentUser(), [token]); // eslint-disable-line react-hooks/exhaustive-deps
  }

  function signOut() {
    const user = currentUser();
    api.setToken(null);
    location.assign(options.signOutUrl(user));
  }

  return { api, currentUser, useCurrentUser, signOut };
}
