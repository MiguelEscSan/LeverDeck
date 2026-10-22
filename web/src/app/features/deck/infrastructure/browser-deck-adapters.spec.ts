import { LAST_PROFILE_KEY, LocalStorageDeckPreferences, VibrationHaptics } from './browser-deck-adapters';

describe('LocalStorageDeckPreferences', () => {
  it('remembers the last profile', () => {
    const items = new Map<string, string>();
    const storage = {
      getItem: (key: string) => items.get(key) ?? null,
      setItem: (key: string, value: string) => void items.set(key, value),
    } as Storage;
    const preferences = new LocalStorageDeckPreferences(() => storage);

    expect(preferences.lastProfileId()).toBeNull();
    preferences.rememberProfile('p2');

    expect(items.get(LAST_PROFILE_KEY)).toBe('p2');
    expect(preferences.lastProfileId()).toBe('p2');
  });

  it('works without storage', () => {
    const preferences = new LocalStorageDeckPreferences(() => {
      throw new DOMException('denied', 'SecurityError');
    });

    expect(() => preferences.rememberProfile('p1')).not.toThrow();
    expect(preferences.lastProfileId()).toBeNull();
  });
});

describe('VibrationHaptics', () => {
  it('vibrates briefly on a tap and in a pattern on failure', () => {
    const patterns: (number | number[])[] = [];
    const haptics = new VibrationHaptics((pattern) => {
      patterns.push(pattern);
      return true;
    });

    haptics.tap();
    haptics.failure();

    expect(patterns).toEqual([10, [30, 50, 30]]);
  });

  it('does nothing where vibration is unavailable', () => {
    expect(() => new VibrationHaptics(null).tap()).not.toThrow();
  });
});
