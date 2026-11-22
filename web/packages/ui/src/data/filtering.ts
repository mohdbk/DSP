// Advanced filtering for every DataTable: typed conditions per column (text, number, date, choice), combined with
// "all" or "any", plus a search box that understands several words and "exact phrases". Pure logic, no UI.

export type ColumnType = 'text' | 'number' | 'date' | 'choice';

export type Operator =
  | 'contains' | 'notContains' | 'equals' | 'startsWith'
  | 'eq' | 'gt' | 'lt' | 'between'
  | 'on' | 'before' | 'after' | 'range' | 'lastDays'
  | 'anyOf' | 'noneOf'
  | 'empty' | 'notEmpty';

export interface Condition {
  id: string;
  column: string;
  op: Operator;
  value?: string;
  value2?: string;
  values?: string[];
}

export interface FilterState { match: 'all' | 'any'; conditions: Condition[] }

export const emptyFilters: FilterState = { match: 'all', conditions: [] };

export const operatorsFor: Record<ColumnType, Operator[]> = {
  text: ['contains', 'notContains', 'equals', 'startsWith', 'empty', 'notEmpty'],
  number: ['eq', 'gt', 'lt', 'between', 'empty', 'notEmpty'],
  date: ['on', 'before', 'after', 'range', 'lastDays', 'empty', 'notEmpty'],
  choice: ['anyOf', 'noneOf', 'empty', 'notEmpty'],
};

/** How many value inputs an operator needs. */
export const valueCount = (op: Operator): 0 | 1 | 2 | 'many' =>
  op === 'empty' || op === 'notEmpty' ? 0 : op === 'between' || op === 'range' ? 2 : op === 'anyOf' || op === 'noneOf' ? 'many' : 1;

const ISO_DATE = /^\d{4}-\d{2}-\d{2}([T ][\d:.]+(Z|[+-]\d{2}:?\d{2})?)?$/;

/** Guesses a column's type from its values when the column doesn't say. */
export function inferType(values: unknown[], declaredChoice: boolean): ColumnType {
  if (declaredChoice) return 'choice';
  const present = values.filter(v => v != null && v !== '' && !Array.isArray(v)).slice(0, 50).map(String);
  if (values.some(Array.isArray)) return 'choice';
  if (present.length === 0) return 'text';
  if (present.every(v => ISO_DATE.test(v))) return 'date';
  if (present.every(v => v.trim() !== '' && !Number.isNaN(Number(v)))) return 'number';
  return 'text';
}

const isEmpty = (v: unknown) => v == null || v === '' || (Array.isArray(v) && v.length === 0);
const asList = (v: unknown): string[] => (Array.isArray(v) ? v.map(String) : isEmpty(v) ? [] : [String(v)]);
const fold = (s: string) => s.toLocaleLowerCase().normalize('NFKD').replace(/[ً-ٰٟ̀-ͯ]/g, '');

/** The calendar day (local time) of an ISO value, as YYYY-MM-DD. */
function day(value: string): string {
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Does one condition hold for a cell value (labels lets text match a choice's translated name)? */
export function testCondition(c: Condition, value: unknown, label: (v: string) => string = v => v, now = new Date()): boolean {
  if (c.op === 'empty') return isEmpty(value);
  if (c.op === 'notEmpty') return !isEmpty(value);
  const values = asList(value);
  switch (c.op) {
    case 'anyOf': return (c.values ?? []).length === 0 || values.some(v => c.values!.includes(v));
    case 'noneOf': return !values.some(v => (c.values ?? []).includes(v));
    case 'contains': case 'notContains': case 'equals': case 'startsWith': {
      const needle = fold(c.value ?? '');
      if (!needle) return true;
      const texts = values.flatMap(v => [fold(v), fold(label(v))]);
      const hit = c.op === 'equals' ? texts.some(x => x === needle)
        : c.op === 'startsWith' ? texts.some(x => x.startsWith(needle))
          : texts.some(x => x.includes(needle));
      return c.op === 'notContains' ? !hit : hit;
    }
    case 'eq': case 'gt': case 'lt': case 'between': {
      const n = Number(values[0]);
      if (Number.isNaN(n) || values.length === 0) return false;
      const a = Number(c.value), b = Number(c.value2);
      if (c.op === 'eq') return c.value === undefined || c.value === '' || n === a;
      if (c.op === 'gt') return c.value === undefined || c.value === '' || n > a;
      if (c.op === 'lt') return c.value === undefined || c.value === '' || n < a;
      return (c.value === undefined || c.value === '' || n >= a) && (c.value2 === undefined || c.value2 === '' || n <= b);
    }
    case 'on': case 'before': case 'after': case 'range': case 'lastDays': {
      const d = values[0] ? day(values[0]) : '';
      if (!d) return false;
      if (c.op === 'lastDays') {
        const n = Number(c.value);
        if (!n) return true;
        const from = new Date(now);
        from.setDate(from.getDate() - n);
        return d >= day(from.toISOString()) && d <= day(now.toISOString());
      }
      const a = c.value ?? '', b = c.value2 ?? '';
      if (c.op === 'on') return !a || d === a;
      if (c.op === 'before') return !a || d < a;
      if (c.op === 'after') return !a || d > a;
      return (!a || d >= a) && (!b || d <= b);
    }
    default:
      return true;
  }
}

/** Search words: spaces separate words, "quoted text" stays together. Every word must be found somewhere in the row. */
export function searchTerms(q: string): string[] {
  const terms: string[] = [];
  for (const m of q.matchAll(/"([^"]+)"|(\S+)/g)) terms.push(fold(m[1] ?? m[2]));
  return terms.filter(Boolean);
}

export interface FilterColumn<T> {
  id: string;
  value: (row: T, index: number) => unknown;
  label?: (v: string) => string;
}

/** Rows matching the search words and the conditions ("all" or "any"). */
export function filterRows<T>(rows: T[], columns: FilterColumn<T>[], q: string, filters: FilterState, now = new Date()): T[] {
  const terms = searchTerms(q);
  const byId = new Map(columns.map(c => [c.id, c]));
  const active = filters.conditions.filter(c => byId.has(c.column));
  return rows.filter((row, index) => {
    if (terms.length) {
      const haystack = columns.flatMap(c => asList(c.value(row, index)).flatMap(v => [fold(v), fold(c.label?.(v) ?? v)])).join('\u0001');
      if (!terms.every(term => haystack.includes(term))) return false;
    }
    if (active.length === 0) return true;
    const results = active.map(c => {
      const col = byId.get(c.column)!;
      return testCondition(c, col.value(row, index), col.label, now);
    });
    return filters.match === 'any' ? results.some(Boolean) : results.every(Boolean);
  });
}

/** Keeps only well-formed conditions (from a URL or saved view). */
export function sanitizeFilters(input: unknown): FilterState | undefined {
  if (!input || typeof input !== 'object') return undefined;
  const raw = input as { match?: unknown; conditions?: unknown };
  const ops = new Set(Object.values(operatorsFor).flat());
  const conditions = Array.isArray(raw.conditions)
    ? raw.conditions.flatMap((c): Condition[] => {
      if (!c || typeof c !== 'object') return [];
      const x = c as Record<string, unknown>;
      if (typeof x.column !== 'string' || typeof x.op !== 'string' || !ops.has(x.op as Operator)) return [];
      return [{
        id: typeof x.id === 'string' ? x.id : Math.random().toString(36).slice(2),
        column: x.column, op: x.op as Operator,
        value: typeof x.value === 'string' ? x.value : undefined,
        value2: typeof x.value2 === 'string' ? x.value2 : undefined,
        values: Array.isArray(x.values) ? x.values.filter((v): v is string => typeof v === 'string') : undefined,
      }];
    })
    : [];
  if (conditions.length === 0) return undefined;
  return { match: raw.match === 'any' ? 'any' : 'all', conditions };
}
