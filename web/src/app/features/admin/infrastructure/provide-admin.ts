import { DOCUMENT } from '@angular/common';
import { type EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import { IconEncoder } from '../application/icon-encoder.port';
import { IdSource } from '../application/id-source.port';
import { CanvasIconEncoder } from './canvas-icon-encoder';
import { CryptoIdSource } from './crypto-id-source';

/** Binds the editor's ports to the browser: ids from the page's `crypto`, icons drawn on a `<canvas>`. */
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
    {
      provide: IconEncoder,
      useFactory: () => {
        const document = inject(DOCUMENT);
        const window = document.defaultView;
        if (!window) {
          throw new Error('LeverDeck needs a browser window.');
        }
        return new CanvasIconEncoder(
          (file) => window.createImageBitmap(file),
          () => document.createElement('canvas'),
        );
      },
    },
  ]);
}
