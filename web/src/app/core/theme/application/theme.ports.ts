import type { ThemePreference } from '../domain/theme';

/** Where the preference survives between visits, on this device only (`localStorage` in the browser). Never throws. */
export abstract class ThemeStore {
  abstract read(): ThemePreference;
  abstract write(preference: ThemePreference): void;
}

/** Puts a preference on screen: the theme tokens and the browser chrome colour. */
export abstract class ThemeTarget {
  abstract apply(preference: ThemePreference): void;
}
