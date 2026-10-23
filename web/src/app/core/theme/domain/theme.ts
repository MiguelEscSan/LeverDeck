/** How this device picks its theme: follow the operating system, or pin dark or light. */
export type ThemePreference = 'system' | 'dark' | 'light';

/** In the order the controls show them. */
export const THEME_PREFERENCES: readonly ThemePreference[] = ['system', 'dark', 'light'];

export const THEME_LABELS: Readonly<Record<ThemePreference, string>> = {
  system: 'System',
  dark: 'Dark',
  light: 'Light',
};

/**
 * The browser chrome colour of each theme: `--ld-color-background` in `styles.scss`. `index.html` repeats these
 * values in its metas and in the script that applies the stored theme before the first paint.
 */
export const THEME_COLORS = { dark: '#2b2b2b', light: '#f2f2f2' } as const;

/** A stored or typed value as a preference; anything unknown follows the system. */
export function parseThemePreference(value: string | null | undefined): ThemePreference {
  return value === 'dark' || value === 'light' ? value : 'system';
}

/** The content of the `theme-color` meta for a dark and for a light system scheme. */
export function themeColors(preference: ThemePreference): { dark: string; light: string } {
  return preference === 'system'
    ? { dark: THEME_COLORS.dark, light: THEME_COLORS.light }
    : { dark: THEME_COLORS[preference], light: THEME_COLORS[preference] };
}
