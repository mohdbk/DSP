// The filter conditions every DataTable sends to its list endpoint: typed conditions per column (text, number, date,
// choice), combined with "all" or "any". The server applies them (ListFields in the services); this file only
// describes them. Pure logic, no UI.

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
