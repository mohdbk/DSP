// A2UI 1.0 (https://a2ui.org) — the screens the chat agent sends, as data. Rendering is in @dsp/ui (A2uiSurface)
// with our AEGOV catalogue; this module only applies messages to a surface's components and data model.

export const A2UI_CATALOG = 'https://moet.gov.ae/dsp/a2ui/catalogs/aegov/v1';

export interface A2uiComponent { id: string; component: string; [prop: string]: unknown }

export interface A2uiSurfaceState {
  surfaceId: string;
  catalogId?: string;
  components: Record<string, A2uiComponent>;
  data: Record<string, unknown>;
}

export type A2uiMessage =
  | { version: string; createSurface: { surfaceId: string; catalogId?: string; dataModel?: Record<string, unknown>; components?: A2uiComponent[] } }
  | { version: string; updateComponents: { surfaceId: string; components: A2uiComponent[] } }
  | { version: string; updateDataModel: { surfaceId: string; path?: string; value: unknown } }
  | { version: string; deleteSurface: { surfaceId: string } };

const pointer = (path: string) => path.split('/').slice(1).map(p => p.replace(/~1/g, '/').replace(/~0/g, '~'));

export function readPath(data: unknown, path: string): unknown {
  return pointer(path).reduce<unknown>((node, key) => (node && typeof node === 'object' ? (node as Record<string, unknown>)[key] : undefined), data);
}

/** A copy of the data with the value at the JSON Pointer replaced (null removes it). */
export function writePath(data: Record<string, unknown>, path: string, value: unknown): Record<string, unknown> {
  const keys = pointer(path);
  if (keys.length === 0) return (value ?? {}) as Record<string, unknown>;
  const copy: Record<string, unknown> = { ...data };
  let node: Record<string, unknown> = copy;
  keys.slice(0, -1).forEach(k => { node[k] = { ...((node[k] as Record<string, unknown>) ?? {}) }; node = node[k] as Record<string, unknown>; });
  const last = keys[keys.length - 1];
  if (value === null) delete node[last]; else node[last] = value;
  return copy;
}

/** Resolves literals and {"@path": "/x"} bindings against the data model, recursively for objects. */
export function resolve(value: unknown, data: unknown): unknown {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    const v = value as Record<string, unknown>;
    if (typeof v['@path'] === 'string') return readPath(data, v['@path'] as string);
    return Object.fromEntries(Object.entries(v).map(([k, x]) => [k, resolve(x, data)]));
  }
  if (Array.isArray(value)) return value.map(x => resolve(x, data));
  return value;
}

/** Applies A2UI messages in order to a set of surfaces. */
export function applyA2ui(surfaces: Record<string, A2uiSurfaceState>, messages: A2uiMessage[]): Record<string, A2uiSurfaceState> {
  const next = { ...surfaces };
  for (const m of messages) {
    if ('createSurface' in m) {
      const c = m.createSurface;
      next[c.surfaceId] = {
        surfaceId: c.surfaceId, catalogId: c.catalogId, data: c.dataModel ?? {},
        components: Object.fromEntries((c.components ?? []).map(x => [x.id, x])),
      };
    } else if ('updateComponents' in m) {
      const s = next[m.updateComponents.surfaceId];
      if (s) next[s.surfaceId] = { ...s, components: { ...s.components, ...Object.fromEntries(m.updateComponents.components.map(x => [x.id, x])) } };
    } else if ('updateDataModel' in m) {
      const s = next[m.updateDataModel.surfaceId];
      if (s) next[s.surfaceId] = { ...s, data: writePath(s.data, m.updateDataModel.path ?? '/', m.updateDataModel.value) };
    } else if ('deleteSurface' in m) {
      delete next[m.deleteSurface.surfaceId];
    }
  }
  return next;
}
