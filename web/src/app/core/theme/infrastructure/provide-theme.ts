import { DOCUMENT } from '@angular/common';
import { type EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import { ThemeStore, ThemeTarget } from '../application/theme.ports';
import { DocumentThemeTarget, LocalStorageThemeStore } from './browser-theme-adapters';

/** Binds the theme ports to the browser: the preference in `localStorage`, the theme on the document. */
export function provideTheme(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: ThemeStore,
      useFactory: () => {
        const window = inject(DOCUMENT).defaultView;
        return new LocalStorageThemeStore(() => window?.localStorage ?? null);
      },
    },
    { provide: ThemeTarget, useFactory: () => new DocumentThemeTarget(inject(DOCUMENT)) },
  ]);
}
