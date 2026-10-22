import { DeckPreferences } from '../application/deck-preferences.port';
import { Haptics } from '../application/haptics.port';

export const LAST_PROFILE_KEY = 'leverdeck.lastProfileId';

/** {@link DeckPreferences} in `localStorage`; blocked storage just means nothing is remembered. */
export class LocalStorageDeckPreferences extends DeckPreferences {
  constructor(private readonly storage: () => Storage | null) {
    super();
  }

  lastProfileId(): string | null {
    try {
      return this.storage()?.getItem(LAST_PROFILE_KEY) || null;
    } catch {
      return null;
    }
  }

  rememberProfile(profileId: string): void {
    try {
      this.storage()?.setItem(LAST_PROFILE_KEY, profileId);
    } catch {
      // Not remembered: the deck opens the first profile next time.
    }
  }
}

type Vibrate = (pattern: number | number[]) => boolean;

/** {@link Haptics} through `navigator.vibrate`, when the browser has it. */
export class VibrationHaptics extends Haptics {
  constructor(private readonly vibrate: Vibrate | null) {
    super();
  }

  tap(): void {
    this.pulse(10);
  }

  failure(): void {
    this.pulse([30, 50, 30]);
  }

  private pulse(pattern: number | number[]): void {
    try {
      this.vibrate?.(pattern);
    } catch {
      // Vibration is a nicety; some browsers throw before a user gesture.
    }
  }
}
