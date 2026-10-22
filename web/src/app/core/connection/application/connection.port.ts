import { InjectionToken, type Signal } from '@angular/core';
import type { Observable } from 'rxjs';
import type { ConnectionStatus } from '../domain/connection-status';
import type { ConnectionTarget } from '../domain/connection-target';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';
import type { ConnectionRejection } from '../domain/rejection';

/**
 * The link to the agent. The adapter owns the transport, authentication and reconnection; nothing above it knows it
 * is a WebSocket. Provided in `app.config.ts` (one instance per scope).
 */
export abstract class ConnectionPort {
  /** `connected` only once the agent accepted the credentials. */
  abstract readonly status: Signal<ConnectionStatus>;

  /** Why the agent refused the connection for good; `null` while it is fine or retrying. Reset by {@link connect}. */
  abstract readonly rejection: Signal<ConnectionRejection | null>;

  /** Every valid message from the agent; invalid frames are dropped by the adapter. */
  abstract readonly messages: Observable<IncomingMessage>;

  /**
   * Starts connecting as `target` and keeps reconnecting until {@link disconnect} or a rejection. Calling it again
   * with the same target is a no-op; another target replaces the current connection.
   */
  abstract connect(target: ConnectionTarget): void;

  /** Closes the link and stops reconnecting. */
  abstract disconnect(): void;

  /** Sends a message; returns `false` (and drops it) when not connected. */
  abstract send(message: OutgoingMessage): boolean;
}

/** The editor's connection to `/admin/ws`; a second instance of the same adapter, bound in `app.config.ts`. */
export const ADMIN_CONNECTION = new InjectionToken<ConnectionPort>('ADMIN_CONNECTION');
