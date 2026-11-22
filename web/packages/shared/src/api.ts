import type { FieldError } from './types';

/** Error raised for non-2xx responses, carrying RFC 7807 problem details from the services. */
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors: FieldError[] = [],
    public correlationId?: string,
  ) {
    super(message);
  }
}

export interface ApiClient {
  get<T>(path: string): Promise<T>;
  post<T>(path: string, body?: unknown): Promise<T>;
  put<T>(path: string, body?: unknown): Promise<T>;
  upload<T>(path: string, file: File): Promise<T>;
  download(path: string): Promise<Blob>;
  token(): string | null;
  setToken(token: string | null): void;
}

/**
 * Same-origin client: each portal's web server forwards /api to the gateway.
 * The platform token is kept in sessionStorage under a key per portal.
 */
export function createApi(storageKey: string, onUnauthorized: () => void): ApiClient {
  const token = () => sessionStorage.getItem(storageKey);
  const setToken = (t: string | null) =>
    t ? sessionStorage.setItem(storageKey, t) : sessionStorage.removeItem(storageKey);

  async function request<T>(method: string, path: string, body?: BodyInit, json = true): Promise<T> {
    const headers: Record<string, string> = {};
    const t = token();
    if (t) headers.Authorization = `Bearer ${t}`;
    if (json && body !== undefined) headers['Content-Type'] = 'application/json';

    const res = await fetch(path, { method, headers, body });
    if (res.status === 401) {
      setToken(null);
      onUnauthorized();
      throw new ApiError(401, 'Your session has ended. Please sign in again.');
    }
    if (!res.ok) {
      let problem: { title?: string; detail?: string; errors?: FieldError[]; correlationId?: string } = {};
      try { problem = await res.json(); } catch { /* not JSON */ }
      throw new ApiError(res.status, problem.detail || problem.title || `Request failed (${res.status})`,
        problem.errors ?? [], problem.correlationId);
    }
    if (res.status === 204) return undefined as T;
    const type = res.headers.get('content-type') ?? '';
    return (type.includes('application/json') ? res.json() : res.blob()) as Promise<T>;
  }

  return {
    get: <T,>(path: string) => request<T>('GET', path),
    post: <T,>(path: string, body?: unknown) =>
      request<T>('POST', path, body === undefined ? undefined : JSON.stringify(body)),
    put: <T,>(path: string, body?: unknown) =>
      request<T>('PUT', path, body === undefined ? undefined : JSON.stringify(body)),
    upload: <T,>(path: string, file: File) => {
      const form = new FormData();
      form.append('file', file);
      return request<T>('POST', path, form, false);
    },
    download: (path: string) => request<Blob>('GET', path),
    token,
    setToken,
  };
}

/** Reads display claims from the platform token (no verification; the services verify it). */
export function readClaims(token: string | null): Record<string, string> | null {
  if (!token) return null;
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(atob(payload).split('').map(c => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join(''));
    const claims = JSON.parse(json);
    if (claims.exp && claims.exp * 1000 < Date.now()) return null;
    return claims;
  } catch {
    return null;
  }
}

export async function openBlob(blob: Blob, fileName?: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.target = '_blank';
  if (fileName && !blob.type.includes('html') && !blob.type.includes('pdf') && !blob.type.startsWith('image/')) {
    a.download = fileName;
  }
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
