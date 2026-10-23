import { pageOf, STORE_APPS_PAGE_SIZE } from './store-apps-page';

const apps = (count: number) =>
  Array.from({ length: count }, (_, index) => ({ name: `App ${String(index + 1).padStart(2, '0')}` }));

describe('pageOf', () => {
  it('shows a page of the apps and how many pages there are', () => {
    const result = pageOf(apps(20), '', 0);

    expect(result.items).toHaveLength(STORE_APPS_PAGE_SIZE);
    expect(result.items[0].name).toBe('App 01');
    expect(result.pageCount).toBe(3);
  });

  it('keeps the page inside the range and shows the rest on the last one', () => {
    expect(pageOf(apps(20), '', 2).items.map((app) => app.name)).toEqual(['App 17', 'App 18', 'App 19', 'App 20']);
    expect(pageOf(apps(20), '', 9).page).toBe(2);
    expect(pageOf(apps(20), '', -1).page).toBe(0);
  });

  it('filters by name ignoring case and accents, and resets to a valid page', () => {
    const named = [{ name: 'Asistencia rápida' }, { name: 'Calculadora' }, { name: 'Bloc de notas' }];

    expect(pageOf(named, 'RAPIDA', 0).items).toEqual([{ name: 'Asistencia rápida' }]);
    expect(pageOf(named, 'calc', 4).items).toEqual([{ name: 'Calculadora' }]);
    expect(pageOf(named, 'zzz', 0)).toEqual({ items: [], page: 0, pageCount: 1 });
  });
});
