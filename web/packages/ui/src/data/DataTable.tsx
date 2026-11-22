import { useMemo, useState, type ReactNode } from 'react';
import { Cell, Column, Row, Table, TableBody, TableHeader, type SortDescriptor } from 'react-aria-components';
import {
  flexRender, getCoreRowModel, getFacetedRowModel, getFacetedUniqueValues, getFilteredRowModel, getPaginationRowModel,
  getSortedRowModel, useReactTable, type ColumnDef, type ColumnFiltersState, type FilterFn, type RowData, type SortingState,
} from '@tanstack/react-table';
import { AegovPagination } from '@aegov/designsystem-react';
import { t } from '@dsp/core';
import { ChoiceField, TextField } from '../forms/fields';
import { EmptyState } from '../feedback/Feedback';

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
  }
}

/** A filter matches the cell's value exactly. */
const equalsValue: FilterFn<unknown> = (row, columnId, value) => value == null || value === '' || String(row.getValue(columnId) ?? '') === value;

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
  /** Column filters as { columnId: value }. */
  filters?: Record<string, string>;
  onFiltersChange?: (filters: Record<string, string>) => void;
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
  const [localFilters, setLocalFilters] = useState<Record<string, string>>({});

  const search = props.search ?? localSearch;
  const page = props.page ?? localPage;
  const sort = props.sort ?? localSort;
  const setSearch = props.onSearchChange ?? setLocalSearch;
  const setPage = props.onPageChange ?? setLocalPage;
  const setSort = props.onSortChange ?? setLocalSort;
  const filters = props.filters ?? localFilters;
  const setFilters = props.onFiltersChange ?? setLocalFilters;
  const sorting = useMemo(() => parseSort(sort), [sort]);
  const columnFilters: ColumnFiltersState = useMemo(
    () => Object.entries(filters).filter(([, v]) => v).map(([id, value]) => ({ id, value })), [filters]);

  const table = useReactTable({
    data,
    columns,
    getRowId,
    state: { globalFilter: search, sorting, columnFilters, pagination: { pageIndex: page - 1, pageSize } },
    defaultColumn: { filterFn: equalsValue as FilterFn<T> },
    globalFilterFn: 'includesString',
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getFacetedRowModel: getFacetedRowModel(),
    getFacetedUniqueValues: getFacetedUniqueValues(),
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
  const filterable = table.getAllLeafColumns().filter(c => c.columnDef.meta?.filter);

  return (
    <div>
      <div className="grid gap-x-4 sm:grid-cols-2 lg:grid-cols-4">
        <TextField id={`${idBase}-search`} type="search" label={t('table.search')} hint={t('table.searchHint')}
          value={search} onChange={q => { setSearch(q); setPage(1); }} />
        {filterable.map(column => {
          const header = column.columnDef.header;
          const name = typeof header === 'string' ? header : column.id;
          const values = [...column.getFacetedUniqueValues().keys()].map(v => String(v ?? '')).filter(Boolean).sort();
          const labelOf = column.columnDef.meta?.filterLabel ?? ((v: string) => v);
          return (
            <ChoiceField key={column.id} id={`${idBase}-filter-${column.id}`} label={name} as="select"
              placeholder={t('table.all')} value={filters[column.id] ?? ''}
              choices={[{ value: '', label: t('table.all') }, ...values.map(v => ({ value: v, label: labelOf(v) }))]}
              onChange={v => { setFilters({ ...filters, [column.id]: v }); setPage(1); }} />
          );
        })}
      </div>

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
          {t('table.results', { n: table.getFilteredRowModel().rows.length })}
        </span>
        {pages > 1 && (
          <AegovPagination pages={pages} pageCurrent={Math.min(page, pages)}
            onAegovPaginationPageChange={e => setPage(e.detail)} />
        )}
      </div>
    </div>
  );
}
