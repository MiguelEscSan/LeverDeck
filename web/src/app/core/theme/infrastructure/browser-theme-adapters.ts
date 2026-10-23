import { ThemeStore, ThemeTarget } from '../application/theme.ports';
import { parseThemePreference, type ThemePreference, themeColors } from '../domain/theme';

/** Also read by the inline script in `index.html`; keep both in step. */
export const THEME_KEY = 'leverdeck.theme';

/** {@link ThemeStore} in `localStorage`; blocked storage just means the theme follows the system. */
export class LocalStorageThemeStore extends ThemeStore {
  constructor(private readonly storage: () => Storage | null) {
    super();
  }

  read(): ThemePreference {
    try {
      return parseThemePreference(this.storage()?.getItem(THEME_KEY));
    } catch {
      return 'system';
    }
  }

  write(preference: ThemePreference): void {
    try {
      this.storage()?.setItem(THEME_KEY, preference);
    } catch {
      // Not remembered: the next visit follows the system again.
    }
  }
}

/**
 * {@link ThemeTarget} on the document: `data-theme` on `<html>` pins the tokens in `styles.scss` (no attribute follows
 * `prefers-color-scheme`), and both `theme-color` metas take the pinned colour.
 */
export class DocumentThemeTarget extends ThemeTarget {
  constructor(private readonly document: Document) {
    super();
  }

  apply(preference: ThemePreference): void {
    const root = this.document.documentElement;
    if (preference === 'system') {
      root.removeAttribute('data-theme');
    } else {
      root.setAttribute('data-theme', preference);
    }
    const colors = themeColors(preference);
    for (const meta of this.document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]')) {
      meta.content = (meta.getAttribute('media') ?? '').includes('light') ? colors.light : colors.dark;
    }
  }
}
