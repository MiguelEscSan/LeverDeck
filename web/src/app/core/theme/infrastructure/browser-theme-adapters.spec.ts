import { THEME_COLORS } from '../domain/theme';
import { DocumentThemeTarget, LocalStorageThemeStore, THEME_KEY } from './browser-theme-adapters';

const memoryStorage = (items = new Map<string, string>()) =>
  ({
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
  }) as Storage;

describe('LocalStorageThemeStore', () => {
  it('remembers the preference', () => {
    const items = new Map<string, string>();
    const store = new LocalStorageThemeStore(() => memoryStorage(items));

    expect(store.read()).toBe('system');
    store.write('light');

    expect(items.get(THEME_KEY)).toBe('light');
    expect(store.read()).toBe('light');
  });

  it('reads an unknown value as system', () => {
    const store = new LocalStorageThemeStore(() => memoryStorage(new Map([[THEME_KEY, 'neon']])));

    expect(store.read()).toBe('system');
  });

  it('works without storage', () => {
    const store = new LocalStorageThemeStore(() => {
      throw new DOMException('denied', 'SecurityError');
    });

    expect(() => store.write('dark')).not.toThrow();
    expect(store.read()).toBe('system');
  });
});

describe('DocumentThemeTarget', () => {
  let doc: Document;

  beforeEach(() => {
    doc = document.implementation.createHTMLDocument('test');
    doc.head.innerHTML = `
      <meta name="theme-color" media="(prefers-color-scheme: dark)" content="${THEME_COLORS.dark}" />
      <meta name="theme-color" media="(prefers-color-scheme: light)" content="${THEME_COLORS.light}" />
    `;
  });

  const metaColors = () =>
    Array.from(doc.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]'), (meta) => meta.content);

  it('pins a theme on the root element and in both theme-color metas', () => {
    new DocumentThemeTarget(doc).apply('light');

    expect(doc.documentElement.dataset['theme']).toBe('light');
    expect(metaColors()).toEqual([THEME_COLORS.light, THEME_COLORS.light]);
  });

  it('follows the system again without the attribute', () => {
    const target = new DocumentThemeTarget(doc);
    target.apply('dark');
    target.apply('system');

    expect(doc.documentElement.hasAttribute('data-theme')).toBe(false);
    expect(metaColors()).toEqual([THEME_COLORS.dark, THEME_COLORS.light]);
  });
});
