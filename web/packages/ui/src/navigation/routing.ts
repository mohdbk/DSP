import { redirect } from '@tanstack/react-router';
import { emptyFilters, sanitizeFilters, type FilterState } from '../data/filtering';

/**
 * A route guard for TanStack Router: send people who aren't signed in to the portal's sign-in page,
 * remembering where they were going.
 */
export function requireSignedIn(isSignedIn: () => boolean, loginPath: string) {
  return ({ location }: { location: { href: string } }) => {
    if (!isSignedIn()) throw redirect({ href: `${loginPath}?returnUrl=${encodeURIComponent(location.href)}` });
  };
}

/** List state (search, page, sort, filters) kept in the URL, so it survives reloads and can be shared. */
export interface ListSearch { q?: string; page?: number; sort?: string; adv?: FilterState }

export function validateListSearch(search: Record<string, unknown>): ListSearch {
  const page = Number(search.page);
  return {
    q: typeof search.q === 'string' && search.q ? search.q : undefined,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: typeof search.sort === 'string' && search.sort ? search.sort : undefined,
    adv: sanitizeFilters(search.adv),
  };
}

/**
 * Connects a DataTable to the URL: pass the route's search and a function that writes it back
 * (e.g. `next => navigate({ to: '.', search: next, replace: true })`).
 */
export function listStateProps(search: ListSearch, write: (next: ListSearch) => void) {
  const update = (patch: Partial<ListSearch>) => write({ ...search, ...patch });
  return {
    search: search.q ?? '',
    onSearchChange: (q: string) => update({ q: q || undefined, page: undefined }),
    page: search.page ?? 1,
    onPageChange: (page: number) => update({ page: page > 1 ? page : undefined }),
    sort: search.sort,
    onSortChange: (sort: string | undefined) => update({ sort, page: undefined }),
    advanced: search.adv ?? emptyFilters,
    onAdvancedChange: (adv: FilterState) => update({ adv: adv.conditions.length ? adv : undefined, page: undefined }),
    onViewChange: (view: { q?: string; sort?: string; advanced?: FilterState }) => write({
      q: view.q || undefined, sort: view.sort, adv: view.advanced?.conditions.length ? view.advanced : undefined,
    }),
  };
}
