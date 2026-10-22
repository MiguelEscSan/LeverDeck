import { DOCUMENT } from '@angular/common';
import { type EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import { IdSource } from '../application/id-source.port';
import { CryptoIdSource } from './crypto-id-source';

/** Binds the editor's ports to the browser: ids from the page's `crypto`. */
export function provideAdmin(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: IdSource,
      useFactory: () => {
        const crypto = inject(DOCUMENT).defaultView?.crypto;
        if (!crypto) {
          throw new Error('LeverDeck needs a browser with Web Crypto.');
        }
        return new CryptoIdSource((bytes) => crypto.getRandomValues(bytes));
      },
    },
  ]);
}
