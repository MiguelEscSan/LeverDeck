import { DOCUMENT } from '@angular/common';
import { type EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import { PairingApi } from '../application/pairing-api.port';
import { AdminTokenStore, DeviceTokenStore } from '../application/token-stores';
import { FetchPairingApi } from './fetch-pairing-api';
import { WebStorageTokenStore } from './web-storage-token-store';

export const DEVICE_TOKEN_KEY = 'leverdeck.deviceToken';
export const ADMIN_TOKEN_KEY = 'leverdeck.adminToken';

const browserWindow = (): Window => {
  const window = inject(DOCUMENT).defaultView;
  if (!window) {
    throw new Error('LeverDeck needs a browser window.');
  }
  return window;
};

/** Binds the token stores to Web Storage and the pairing API to `fetch` on the page origin. */
export function provideAuth(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: DeviceTokenStore,
      useFactory: () => {
        const window = browserWindow();
        return new WebStorageTokenStore(() => window.localStorage, DEVICE_TOKEN_KEY);
      },
    },
    {
      provide: AdminTokenStore,
      useFactory: () => {
        const window = browserWindow();
        return new WebStorageTokenStore(() => window.sessionStorage, ADMIN_TOKEN_KEY);
      },
    },
    {
      provide: PairingApi,
      useFactory: () => {
        const window = browserWindow();
        return new FetchPairingApi((input, init) => window.fetch(input, init));
      },
    },
  ]);
}
