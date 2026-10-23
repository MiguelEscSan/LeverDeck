import { parseThemePreference, THEME_COLORS, themeColors } from './theme';

describe('parseThemePreference', () => {
  it('keeps a known preference', () => {
    expect(parseThemePreference('dark')).toBe('dark');
    expect(parseThemePreference('light')).toBe('light');
    expect(parseThemePreference('system')).toBe('system');
  });

  it('falls back to system for anything else', () => {
    expect(parseThemePreference(null)).toBe('system');
    expect(parseThemePreference('')).toBe('system');
    expect(parseThemePreference('Dark')).toBe('system');
    expect(parseThemePreference('sepia')).toBe('system');
  });
});

describe('themeColors', () => {
  it('follows the system with one colour per scheme', () => {
    expect(themeColors('system')).toEqual({ dark: THEME_COLORS.dark, light: THEME_COLORS.light });
  });

  it('uses the pinned theme for both schemes', () => {
    expect(themeColors('dark')).toEqual({ dark: THEME_COLORS.dark, light: THEME_COLORS.dark });
    expect(themeColors('light')).toEqual({ dark: THEME_COLORS.light, light: THEME_COLORS.light });
  });
});
