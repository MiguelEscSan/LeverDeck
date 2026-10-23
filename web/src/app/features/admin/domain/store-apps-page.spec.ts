import { pageOf, pageOfApps, REGISTERED_APPS_PAGE_SIZE, STORE_APPS_PAGE_SIZE } from './store-apps-page';

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
    expect(pageOf(named, 'zzz', 0)).toEqual({ items: [], page: 0, pageCount: 1, total: 0 });
  });
});

describe('pageOfApps', () => {
  const registered = (count: number) =>
    Array.from({ length: count }, (_, index) => ({
      appId: `a${index}`,
      displayName: `App ${index + 1}`,
      path: `C:\\Apps\\app${index + 1}.exe`,
    }));

  it('shows a page of the registered apps', () => {
    const result = pageOfApps(registered(11), '', 1);

    expect(result.items).toHaveLength(11 - REGISTERED_APPS_PAGE_SIZE);
    expect(result.page).toBe(1);
    expect(result.pageCount).toBe(2);
    expect(result.total).toBe(11);
  });

  it('finds an app by its name or its path, ignoring case and accents', () => {
    const apps = [
      { appId: 'a1', displayName: 'Música', path: 'shell:AppsFolder\\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify' },
      { appId: 'a2', displayName: 'OBS', path: 'C:\\Program Files\\obs-studio\\bin\\64bit\\obs64.exe' },
    ];

    expect(pageOfApps(apps, 'MUSICA', 0).items.map((app) => app.appId)).toEqual(['a1']);
    expect(pageOfApps(apps, 'obs-studio', 3).items.map((app) => app.appId)).toEqual(['a2']);
    expect(pageOfApps(apps, 'spotify', 0).items.map((app) => app.appId)).toEqual(['a1']);
  });
});
