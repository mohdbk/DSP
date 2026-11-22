import { createContext, useContext, type ReactNode } from 'react';
import type { ApiClient } from '@dsp/core';

const ApiContext = createContext<ApiClient | null>(null);

/** Gives shared components (tables, saved views) the running portal's API client. PortalFrame provides it. */
export function ApiProvider({ api, children }: { api: ApiClient; children: ReactNode }) {
  return <ApiContext.Provider value={api}>{children}</ApiContext.Provider>;
}

export function useApi(): ApiClient {
  const api = useContext(ApiContext);
  if (!api) throw new Error('useApi needs an ApiProvider (PortalFrame provides one).');
  return api;
}
