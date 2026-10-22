import type { Signal } from '@angular/core';
import type { Observable } from 'rxjs';
import type { ConnectionStatus } from '../domain/connection-status';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';

/**
 * The link to the agent. The adapter owns the transport and reconnection; nothing above it knows it is a WebSocket.
 * Provided in `app.config.ts`.
 */
export abstract class ConnectionPort {
  abstract readonly status: Signal<ConnectionStatus>;

  /** Every valid message from the agent; invalid frames are dropped by the adapter. */
  abstract readonly messages: Observable<IncomingMessage>;

  /** Starts connecting and keeps reconnecting until {@link disconnect}. Calling it again is a no-op. */
  abstract connect(): void;

  /** Closes the link and stops reconnecting. */
  abstract disconnect(): void;

  /** Sends a message; returns `false` (and drops it) when not connected. */
  abstract send(message: OutgoingMessage): boolean;
}
