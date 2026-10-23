/** How many Store apps one page of the list shows. */
export const STORE_APPS_PAGE_SIZE = 8;

export interface StoreAppsPage<T> {
  readonly items: readonly T[];
  /** The page shown, inside the range of pages (0-based). */
  readonly page: number;
  /** At least 1, so an empty result still shows one page. */
  readonly pageCount: number;
}

/** Lowercase without accents, so "rapida" finds "rápida" and "ñ" matches "n". */
export function foldText(text: string): string {
  return text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
}

/** The apps whose name contains the query, cut into pages. The page is clamped to the pages that exist. */
export function pageOf<T extends { readonly name: string }>(
  apps: readonly T[],
  query: string,
  page: number,
  pageSize = STORE_APPS_PAGE_SIZE,
): StoreAppsPage<T> {
  const needle = foldText(query.trim());
  const matches = needle === '' ? apps : apps.filter((app) => foldText(app.name).includes(needle));
  const pageCount = Math.max(1, Math.ceil(matches.length / pageSize));
  const current = Math.min(Math.max(page, 0), pageCount - 1);
  return {
    items: matches.slice(current * pageSize, (current + 1) * pageSize),
    page: current,
    pageCount,
  };
}
