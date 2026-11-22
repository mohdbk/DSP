import { useMemo, useState, type ReactNode } from 'react';
import { Cell, Column, Row, Table, TableBody, TableHeader, type SortDescriptor } from 'react-aria-components';
import {
  flexRender, getCoreRowModel, getPaginationRowModel, getSortedRowModel, useReactTable, type ColumnDef, type RowData,
  type SortingState,
} from '@tanstack/react-table';
import { AegovButton, AegovPagination } from '@aegov/designsystem-react';
import { t } from '@dsp/core';
import { TextField } from '../forms/fields';
import { EmptyState } from '../feedback/Feedback';
import { Funnel, IconLabel } from '../icons';
import { FilterChips, FilterDialog, type FilterableColumn } from './FilterBar';
import { SavedViews } from './SavedViews';
import { emptyFilters, filterRows, inferType, type ColumnType, type FilterColumn, type FilterState } from './filtering';

// The design system has no table. This one renders with React Aria (keyboard navigation, row actions,
// sortable columns, screen-reader support, right-to-left) and organises data with TanStack Table
// (sorting, search, paging). Styled with AEGOV tokens; paging and search use AEGOV components.

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
  data: T[];
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
  /** Earlier simple filters as { columnId: value } (read as "is any of" conditions). */
  filters?: Record<string, string>;
  onFiltersChange?: (filters: Record<string, string>) => void;
  /** Advanced filters (conditions on any column), e.g. kept in the URL. Uncontrolled when omitted. */
  advanced?: FilterState;
  onAdvancedChange?: (filters: FilterState) => void;
  emptyTitle: string;
  emptyHint?: ReactNode;
}

function parseSort(sort?: string): SortingState {
  if (!sort) return [];
  return sort.startsWith('-') ? [{ id: sort.slice(1), desc: true }] : [{ id: sort, desc: false }];
}

/**
 * Every list in both portals: search across all columns, sort by any column, filter by columns with repeating
 * values, and page through results.
 */
export function DataTable<T>(props: DataTableProps<T>) {
  const { label, data, columns, getRowId, onRowAction, pageSize = 10, emptyTitle, emptyHint } = props;
  const [localSearch, setLocalSearch] = useState('');
  const [localPage, setLocalPage] = useState(1);
  const [localSort, setLocalSort] = useState<string>();
  const [localAdvanced, setLocalAdvanced] = useState<FilterState>(emptyFilters);
  const [filterOpen, setFilterOpen] = useState(false);

  const search = props.search ?? localSearch;
  const page = props.page ?? localPage;
  const sort = props.sort ?? localSort;
  const setSearch = props.onSearchChange ?? setLocalSearch;
  const setPage = props.onPageChange ?? setLocalPage;
  const setSort = props.onSortChange ?? setLocalSort;
  const legacy = props.filters ?? {};
  const advanced: FilterState = props.advanced ?? (props.onAdvancedChange ? emptyFilters : localAdvanced);
  // Simple filters from older links become "is any of" conditions until the advanced filters are changed.
  const advancedKey = JSON.stringify(advanced);
  const legacyKey = JSON.stringify(legacy);
  const effective: FilterState = useMemo(() => (advanced.conditions.length > 0 || Object.keys(legacy).length === 0 ? advanced : {
    match: 'all',
    conditions: Object.entries(legacy).filter(([, v]) => v).map(([column, v]) => ({ id: column, column, op: 'anyOf' as const, values: v.split(',') })),
  }), [advancedKey, legacyKey]); // eslint-disable-line react-hooks/exhaustive-deps
  const setAdvanced = (next: FilterState) => {
    (props.onAdvancedChange ?? setLocalAdvanced)(next);
    if (Object.keys(legacy).length) props.onFiltersChange?.({});
    setPage(1);
  };
  const sorting = useMemo(() => parseSort(sort), [sort]);

  // Columns that can be searched and filtered: their value, display label and type.
  const fields = useMemo(() => columns.flatMap(c => {
    const def = c as ColumnDef<T, unknown> & { accessorFn?: (row: T, index: number) => unknown };
    if (!def.accessorFn || c.meta?.filterable === false || !c.id) return [];
    const header = typeof c.header === 'string' ? c.header : c.id;
    const values = data.map((row, i) => def.accessorFn!(row, i));
    const type = c.meta?.type ?? inferType(values, !!c.meta?.filter);
    const labelOf = c.meta?.filterLabel;
    const options = type === 'choice'
      ? (c.meta?.filterOptions ?? [...new Set(values.flatMap(valuesOf))].filter(Boolean).sort())
        .map(v => ({ value: v, label: labelOf ? labelOf(v) : v }))
      : undefined;
    const filterable: FilterableColumn = { id: c.id, label: header, type, options };
    const searchable: FilterColumn<T> = { id: c.id, value: def.accessorFn, label: labelOf };
    return [{ filterable, searchable }];
  }), [columns, data]);
  const filterColumns = useMemo(() => fields.map(f => f.filterable), [fields]);

  const filtered = useMemo(() => filterRows(data, fields.map(f => f.searchable), search, effective),
    [data, fields, search, effective]);

  const table = useReactTable({
    data: filtered,
    columns,
    getRowId,
    state: { sorting, pagination: { pageIndex: page - 1, pageSize } },
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    autoResetPageIndex: false,
  });

  const pages = Math.max(1, table.getPageCount());
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
            value={search} onChange={q => { setSearch(q); setPage(1); }} />
        </div>
        <div className="mb-5 flex flex-wrap gap-2 pb-7">
          {filterColumns.length > 0 && (
            <AegovButton appearance="outline" onAegovButtonClick={() => setFilterOpen(true)}>
              <IconLabel icon={Funnel}>{effective.conditions.length ? t('table.filtersCount', { n: effective.conditions.length }) : t('table.filters')}</IconLabel>
            </AegovButton>
          )}
          <SavedViews storageKey={`dsp.views.${idBase}`} current={{ q: search, sort, filters: effective }}
            onApply={v => { setSearch(v.q ?? ''); setSort(v.sort); setAdvanced(v.filters ?? emptyFilters); }} />
        </div>
      </div>
      <FilterChips value={effective} columns={filterColumns} onChange={setAdvanced} />
      <FilterDialog open={filterOpen} value={effective} columns={filterColumns} icon={Funnel}
        onApply={setAdvanced} onClose={() => setFilterOpen(false)} />

      <div className="overflow-x-auto rounded-xl border border-aeblack-100 bg-whitely-50">
        <Table aria-label={label} className="w-full border-collapse text-start"
          sortDescriptor={descriptor}
          onSortChange={d => { setSort(d.direction === 'descending' ? `-${String(d.column)}` : String(d.column)); setPage(1); }}
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
          <TableBody renderEmptyState={() => <EmptyState title={emptyTitle}>{emptyHint}</EmptyState>}>
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
          {t('table.results', { n: filtered.length })}
        </span>
        {pages > 1 && (
          <AegovPagination pages={pages} pageCurrent={Math.min(page, pages)}
            onAegovPaginationPageChange={e => setPage(e.detail)} />
        )}
      </div>
    </div>
  );
}
