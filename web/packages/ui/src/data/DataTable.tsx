import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Cell, Column, Row, Table, TableBody, TableHeader, type SortDescriptor } from 'react-aria-components';
import {
  flexRender, getCoreRowModel, useReactTable, type ColumnDef, type RowData, type SortingState,
} from '@tanstack/react-table';
import { AegovButton, AegovPagination } from '@aegov/designsystem-react';
import { loc, t, type ListParams, type PagedList } from '@dsp/core';
import { TextField } from '../forms/fields';
import { EmptyState, Loading, Notice } from '../feedback/Feedback';
import { Funnel, IconLabel } from '../icons';
import { FilterChips, FilterDialog, type FilterableColumn } from './FilterBar';
import { SavedViews } from './SavedViews';
import { useApi } from './ApiContext';
import { emptyFilters, inferType, type ColumnType, type FilterState } from './filtering';

// The design system has no table. This one renders with React Aria (keyboard navigation, row actions,
// sortable columns, screen-reader support, right-to-left) and TanStack Table (columns and cells); the list endpoint
// searches, filters, sorts and pages on the server. Styled with AEGOV tokens; paging and search use AEGOV components.

export type { ColumnDef } from '@tanstack/react-table';

declare module '@tanstack/react-table' {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData extends RowData, TValue> {
    /** Offer a filter with this column's distinct values (status, service, process…). */
    filter?: boolean;
    /** How a filter value reads, e.g. a translated status. */
    filterLabel?: (value: string) => string;
    /** Let people pick several values (rows matching any of them). The cell value may also be a list. */
    multiple?: boolean;
    /** The filter's values in this order, instead of the distinct values found in the rows. */
    filterOptions?: string[];
    /** How the column filters (guessed from its values when not given: dates, numbers, or text). */
    type?: ColumnType;
    /** Leave the column out of filters and search (e.g. action columns). */
    filterable?: boolean;
  }
}

/** The values of a cell, which is one value or a list (e.g. the portals a text is used in). */
const valuesOf = (value: unknown): string[] => (Array.isArray(value) ? value.map(String) : [String(value ?? '')]);

export interface DataTableProps<T> {
  /** Accessible name of the table. */
  label: string;
  /**
   * Where the rows come from: a list endpoint (GET, answering { items, page, pageSize, total, facets }) or a function
   * that loads one page. Search, filters, sorting and paging always run on the server.
   */
  source: string | ((params: ListParams) => Promise<PagedList<T>>);
  /** Names the list for saved views, e.g. "backoffice.applications". */
  listKey: string;
  /** Change it to load the current page again (after an edit). */
  reloadKey?: unknown;
  /** Called with each page loaded (e.g. to show the total in a badge). */
  onLoad?: (page: PagedList<T>) => void;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  columns: ColumnDef<T, any>[];
  getRowId: (row: T) => string;
  /** Opens a row (click, Enter). */
  onRowAction?: (row: T) => void;
  pageSize?: number;
  /** Controlled list state, e.g. from the URL. Uncontrolled when omitted. */
  search?: string;
  onSearchChange?: (q: string) => void;
  page?: number;
  onPageChange?: (page: number) => void;
  /** "column" for ascending, "-column" for descending. */
  sort?: string;
  onSortChange?: (sort: string | undefined) => void;
  /** Filter conditions (on any column), e.g. kept in the URL. Uncontrolled when omitted. */
  advanced?: FilterState;
  onAdvancedChange?: (filters: FilterState) => void;
  /** Sets search, sort and filters at once (applying a saved view); listStateProps provides it. */
  onViewChange?: (view: { q?: string; sort?: string; advanced?: FilterState }) => void;
  emptyTitle: string;
  emptyHint?: ReactNode;
}

function parseSort(sort?: string): SortingState {
  if (!sort) return [];
  return sort.startsWith('-') ? [{ id: sort.slice(1), desc: true }] : [{ id: sort, desc: false }];
}

/** Waits until the value has stopped changing for a moment (typing in the search box). */
function useSettled<V>(value: V, ms = 300): V {
  const [settled, setSettled] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), ms);
    return () => clearTimeout(timer);
  }, [value, ms]);
  return settled;
}

/**
 * Every list in both portals: search across columns, filter conditions on any column, sorting and paging, all run by
 * the list's endpoint on the server, plus each person's saved views.
 */
export function DataTable<T>(props: DataTableProps<T>) {
  const { label, source, listKey, reloadKey, onLoad, columns, getRowId, onRowAction, pageSize = 10, emptyTitle, emptyHint } = props;
  const api = useApi();
  const [localSearch, setLocalSearch] = useState('');
  const [localPage, setLocalPage] = useState(1);
  const [localSort, setLocalSort] = useState<string>();
  const [localAdvanced, setLocalAdvanced] = useState<FilterState>(emptyFilters);
  const [filterOpen, setFilterOpen] = useState(false);
  const [result, setResult] = useState<PagedList<T>>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const search = props.search ?? localSearch;
  const page = props.page ?? localPage;
  const sort = props.sort ?? localSort;
  const setSearch = props.onSearchChange ?? setLocalSearch;
  const setPage = props.onPageChange ?? setLocalPage;
  const setSort = props.onSortChange ?? setLocalSort;
  const advanced: FilterState = props.advanced ?? (props.onAdvancedChange ? emptyFilters : localAdvanced);
  // Controlled state (listStateProps) goes back to page 1 itself; writing the page as well would undo the change.
  const setAdvanced = (next: FilterState) => {
    if (props.onAdvancedChange) props.onAdvancedChange(next);
    else { setLocalAdvanced(next); setLocalPage(1); }
  };
  const applyView = (v: { q?: string; sort?: string; filter?: FilterState }) => {
    if (props.onViewChange) props.onViewChange({ q: v.q, sort: v.sort, advanced: v.filter });
    else { setLocalSearch(v.q ?? ''); setLocalSort(v.sort); setLocalAdvanced(v.filter ?? emptyFilters); setLocalPage(1); }
  };
  const sorting = useMemo(() => parseSort(sort), [sort]);

  // Ask the server for the page. The search box waits until typing pauses; stale answers are dropped.
  const q = useSettled(search);
  const filterKey = JSON.stringify(advanced);
  const loadRef = useRef(onLoad);
  loadRef.current = onLoad;
  useEffect(() => {
    let current = true;
    setLoading(true);
    const params: ListParams = { q, sort, page, pageSize, filter: advanced.conditions.length ? advanced : undefined };
    (typeof source === 'string' ? api.page<T>(source, params) : source(params))
      .then(r => { if (current) { setResult(r); setError(undefined); loadRef.current?.(r); } })
      .catch(e => { if (current) setError((e as Error).message); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [api, typeof source === 'string' ? source : 'fn', q, sort, page, pageSize, filterKey, reloadKey]); // eslint-disable-line react-hooks/exhaustive-deps

  const rowsData = useMemo(() => result?.items ?? [], [result]);

  // Columns that can be searched and filtered: label, type and, for choices, the values the server offers.
  const filterColumns = useMemo(() => columns.flatMap((c): FilterableColumn[] => {
    const def = c as ColumnDef<T, unknown> & { accessorFn?: (row: T, index: number) => unknown };
    if (!def.accessorFn || c.meta?.filterable === false || !c.id) return [];
    const header = typeof c.header === 'string' ? c.header : c.id;
    const values = rowsData.map((row, i) => def.accessorFn!(row, i));
    const type = c.meta?.type ?? inferType(values, !!c.meta?.filter);
    const labelOf = c.meta?.filterLabel;
    const facet = result?.facets?.[c.id];
    const options = type === 'choice'
      ? (facet
        ? facet.map(o => ({ value: o.value, label: o.label ? loc(o.label) : labelOf ? labelOf(o.value) : o.value }))
        : (c.meta?.filterOptions ?? [...new Set(values.flatMap(valuesOf))].filter(Boolean).sort())
          .map(v => ({ value: v, label: labelOf ? labelOf(v) : v })))
      : undefined;
    return [{ id: c.id, label: header, type, options }];
  }), [columns, rowsData, result]);

  const table = useReactTable({
    data: rowsData,
    columns,
    getRowId,
    state: { sorting },
    manualSorting: true,
    manualPagination: true,
    getCoreRowModel: getCoreRowModel(),
  });

  const total = result?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const headers = table.getHeaderGroups()[0]?.headers ?? [];
  const rows = table.getRowModel().rows;
  const descriptor: SortDescriptor | undefined = sorting[0]
    ? { column: sorting[0].id, direction: sorting[0].desc ? 'descending' : 'ascending' }
    : undefined;

  const idBase = label.replace(/\W+/g, '-').toLowerCase();

  return (
    <div>
      <div className="flex flex-wrap items-end gap-x-3">
        <div className="min-w-56 flex-1">
          <TextField id={`${idBase}-search`} type="search" label={t('table.search')} hint={t('table.searchHint')}
            value={search} onChange={q => { setSearch(q); if (!props.onSearchChange) setLocalPage(1); }} />
        </div>
        <div className="mb-5 flex flex-wrap gap-2 pb-7">
          {filterColumns.length > 0 && (
            <AegovButton appearance="outline" onAegovButtonClick={() => setFilterOpen(true)}>
              <IconLabel icon={Funnel}>{advanced.conditions.length ? t('table.filtersCount', { n: advanced.conditions.length }) : t('table.filters')}</IconLabel>
            </AegovButton>
          )}
          <SavedViews listKey={listKey} current={{ q: search || undefined, sort, filter: advanced.conditions.length ? advanced : undefined }}
            onApply={applyView} />
        </div>
      </div>
      <FilterChips value={advanced} columns={filterColumns} onChange={setAdvanced} />
      <FilterDialog open={filterOpen} value={advanced} columns={filterColumns} icon={Funnel}
        onApply={setAdvanced} onClose={() => setFilterOpen(false)} />

      {error && <Notice kind="error">{error}</Notice>}
      <div className="relative overflow-x-auto rounded-xl border border-aeblack-100 bg-whitely-50" aria-busy={loading}>
        {loading && <div className="absolute inset-x-0 top-0 z-10"><Loading /></div>}
        <Table aria-label={label} className="w-full border-collapse text-start"
          sortDescriptor={descriptor}
          onSortChange={d => { setSort(d.direction === 'descending' ? `-${String(d.column)}` : String(d.column)); if (!props.onSortChange) setLocalPage(1); }}
          onRowAction={onRowAction ? key => { const row = table.getRow(String(key)); if (row) onRowAction(row.original); } : undefined}>
          <TableHeader>
            {headers.map((header, i) => (
              <Column key={header.id} id={header.id} isRowHeader={i === 0} allowsSorting={header.column.getCanSort()}
                className="cursor-default border-b border-aeblack-100 bg-aeblack-50 px-4 py-3 text-start text-sm font-semibold text-aeblack-700 outline-none data-[focus-visible]:outline-2 data-[focus-visible]:-outline-offset-2 data-[focus-visible]:outline-primary-500">
                {({ allowsSorting, sortDirection }) => (
                  <span className="inline-flex items-center gap-1">
                    {flexRender(header.column.columnDef.header, header.getContext())}
                    {allowsSorting && (
                      <span aria-hidden="true" className={sortDirection ? 'text-primary-600' : 'text-aeblack-300'}>
                        {sortDirection === 'descending' ? '▼' : sortDirection === 'ascending' ? '▲' : '↕'}
                      </span>
                    )}
                    {sortDirection && (
                      <span className="sr-only">{sortDirection === 'descending' ? t('table.sortDescending') : t('table.sortAscending')}</span>
                    )}
                  </span>
                )}
              </Column>
            ))}
          </TableHeader>
          <TableBody renderEmptyState={() => (loading && !result ? <div className="h-24" /> : <EmptyState title={emptyTitle}>{emptyHint}</EmptyState>)}>
            {rows.map(row => (
              <Row key={row.id} id={row.id}
                className={`border-b border-aeblack-100 outline-none last:border-0 data-[focus-visible]:outline-2 data-[focus-visible]:-outline-offset-2 data-[focus-visible]:outline-primary-500 ${onRowAction ? 'cursor-pointer data-[hovered]:bg-primary-50' : ''}`}>
                {row.getVisibleCells().map(cell => (
                  <Cell key={cell.id} className="px-4 py-3 align-middle text-sm text-aeblack-800">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </Cell>
                ))}
              </Row>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
        <span className="text-sm text-aeblack-600" aria-live="polite">
          {result && t('table.results', { n: total })}
        </span>
        {pages > 1 && (
          <AegovPagination pages={pages} pageCurrent={Math.min(page, pages)}
            onAegovPaginationPageChange={e => setPage(e.detail)} />
        )}
      </div>
    </div>
  );
}
