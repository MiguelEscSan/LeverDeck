import { DOCUMENT } from '@angular/common';
import { type EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import { DeckPreferences } from '../application/deck-preferences.port';
import { Haptics } from '../application/haptics.port';
import { LocalStorageDeckPreferences, VibrationHaptics } from './browser-deck-adapters';

/** Binds the deck's ports to the browser: preferences in `localStorage`, haptics through `navigator.vibrate`. */
export function provideDeck(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: DeckPreferences,
      useFactory: () => {
        const window = inject(DOCUMENT).defaultView;
        return new LocalStorageDeckPreferences(() => window?.localStorage ?? null);
      },
    },
    {
      provide: Haptics,
      useFactory: () => {
        const navigator = inject(DOCUMENT).defaultView?.navigator;
        const vibrate =
          navigator && typeof navigator.vibrate === 'function' ? navigator.vibrate.bind(navigator) : null;
        return new VibrationHaptics(vibrate);
      },
    },
  ]);
}
