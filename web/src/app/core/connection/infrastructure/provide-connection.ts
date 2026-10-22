import { DOCUMENT } from '@angular/common';
import {
  type EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { ConnectionFacade } from '../application/connection.facade';
import { ConnectionPort } from '../application/connection.port';
import { browserNetwork } from './browser-network';
import { socketUrl } from './socket-url';
import { WebSocketConnection } from './websocket-connection';

/** Binds {@link ConnectionPort} to the WebSocket adapter on the page origin and starts it with the app. */
export function provideConnection(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: ConnectionPort,
      useFactory: () => {
        const window = inject(DOCUMENT).defaultView;
        if (!window) {
          throw new Error('LeverDeck needs a browser window to connect to the agent.');
        }
        return new WebSocketConnection({
          url: socketUrl(window.location),
          createSocket: (url) => new window.WebSocket(url),
          network: browserNetwork(window),
        });
      },
    },
    provideAppInitializer(() => inject(ConnectionFacade).start()),
  ]);
}
