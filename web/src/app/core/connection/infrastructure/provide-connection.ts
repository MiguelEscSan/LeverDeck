import { DOCUMENT } from '@angular/common';
import { type EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import { ADMIN_CONNECTION, ConnectionPort } from '../application/connection.port';
import { browserNetwork } from './browser-network';
import { WebSocketConnection } from './websocket-connection';

/** The agent's liveness endpoint; any HTTP answer from it means the agent is up. */
export const HEALTH_PATH = '/health';

const createConnection = (): WebSocketConnection => {
  const window = inject(DOCUMENT).defaultView;
  if (!window) {
    throw new Error('LeverDeck needs a browser window to connect to the agent.');
  }
  return new WebSocketConnection({
    location: window.location,
    createSocket: (url) => new window.WebSocket(url),
    network: browserNetwork(window),
    agentReachable: () =>
      window
        .fetch(HEALTH_PATH, { cache: 'no-store' })
        .then((response) => response.ok)
        .catch(() => false),
  });
};

/**
 * Binds the device ({@link ConnectionPort}, `/ws`) and editor ({@link ADMIN_CONNECTION}, `/admin/ws`) connections to
 * WebSocket adapters on the page origin. Nothing connects on its own: the deck and the editor pages start the one
 * they need, once they have its token.
 */
export function provideConnection(): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: ConnectionPort, useFactory: createConnection },
    { provide: ADMIN_CONNECTION, useFactory: createConnection },
  ]);
}
