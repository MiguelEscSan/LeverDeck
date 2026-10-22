import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { PROTOCOL_VERSION } from '../../protocol';
import { ConnectionPort } from '../application/connection.port';
import { type BackoffPolicy, DEFAULT_BACKOFF, reconnectDelay } from '../domain/backoff';
import type { ConnectionStatus } from '../domain/connection-status';
import { type ConnectionTarget, sameTarget } from '../domain/connection-target';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';
import {
  type ConnectionRejection,
  rejectionFromClose,
  rejectionFromError,
} from '../domain/rejection';
import { requestIds } from '../domain/request-ids';
import { adminSocketUrl, type PageLocation, socketUrl } from './socket-url';
import { parseIncoming, serializeOutgoing } from './wire-format';

/** The subset of the browser `WebSocket` the adapter uses, so tests can drive a fake one. */
export interface WebSocketLike {
  readonly readyState: number;
  onopen: ((event: Event) => void) | null;
  onclose: ((event: CloseEvent) => void) | null;
  onerror: ((event: Event) => void) | null;
  onmessage: ((event: MessageEvent) => void) | null;
  send(data: string): void;
  close(code?: number, reason?: string): void;
}

/** Whether the device has a network at all (`navigator.onLine` and its events in the browser). */
export interface NetworkStatus {
  isOnline(): boolean;
  /** Calls `onChange` on every change; returns a function that stops watching. */
  watch(onChange: (online: boolean) => void): () => void;
}

export interface WebSocketConnectionOptions {
  /** The page origin; the agent serves the sockets next to the PWA. */
  readonly location: PageLocation;
  readonly createSocket: (url: string) => WebSocketLike;
  readonly network: NetworkStatus;
  /**
   * Whether the agent answers plain HTTP. The agent refuses a wrong admin token with `401` before the upgrade, which
   * a browser reports exactly like an unreachable host; when the agent is up, that refusal is the token.
   */
  readonly agentReachable?: () => Promise<boolean>;
  /** Request ids for `connectDevice`. */
  readonly ids?: () => string;
  /** A value in [0, 1] for the backoff jitter. Defaults to `Math.random`. */
  readonly random?: () => number;
  readonly backoff?: BackoffPolicy;
}

const OPEN = 1;
const NORMAL_CLOSURE = 1000;

/**
 * The WebSocket adapter for {@link ConnectionPort}: one socket at a time, authentication per target, reconnection
 * with exponential backoff and jitter (capped at 30 s by default), and an `offline` pause while the device has no
 * network. A rejection (1008 with `unauthorized`, `deviceRevoked` or `protocolMismatch`) stops it for good: retrying
 * the same credentials can never succeed, so there is no retry loop.
 */
export class WebSocketConnection extends ConnectionPort {
  private readonly state = signal<ConnectionStatus>('offline');
  private readonly rejected = signal<ConnectionRejection | null>(null);
  private readonly incoming = new Subject<IncomingMessage>();
  private readonly random: () => number;
  private readonly backoff: BackoffPolicy;
  private readonly ids: () => string;

  private target: ConnectionTarget | null = null;
  private socket: WebSocketLike | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private attempt = 0;
  /** Bumped on every open and disconnect, so a late async answer can tell it is stale. */
  private generation = 0;
  private running = false;
  private stopWatchingNetwork: (() => void) | null = null;

  readonly status = this.state.asReadonly();
  readonly rejection = this.rejected.asReadonly();
  readonly messages = this.incoming.asObservable();

  constructor(private readonly options: WebSocketConnectionOptions) {
    super();
    this.random = options.random ?? Math.random;
    this.backoff = options.backoff ?? DEFAULT_BACKOFF;
    this.ids = options.ids ?? requestIds();
  }

  connect(target: ConnectionTarget): void {
    if (this.running && sameTarget(this.target, target)) {
      return;
    }
    if (this.running) {
      this.disconnect();
    }
    this.target = target;
    this.running = true;
    this.attempt = 0;
    this.rejected.set(null);
    this.stopWatchingNetwork = this.options.network.watch((online) =>
      this.onNetworkChange(online),
    );

    if (this.options.network.isOnline()) {
      this.open();
    } else {
      this.state.set('offline');
    }
  }

  disconnect(): void {
    this.generation++;
    this.running = false;
    this.stopWatchingNetwork?.();
    this.stopWatchingNetwork = null;
    this.cancelRetry();
    this.closeSocket();
    this.state.set('offline');
  }

  send(message: OutgoingMessage): boolean {
    if (this.state() !== 'connected' || this.socket?.readyState !== OPEN) {
      return false;
    }
    this.socket.send(serializeOutgoing(message));
    return true;
  }

  private open(): void {
    const target = this.target;
    if (!target) {
      return;
    }
    this.cancelRetry();
    this.closeSocket();
    this.generation++;
    this.state.set('reconnecting');

    const url =
      target.scope === 'admin'
        ? adminSocketUrl(this.options.location, target.token)
        : socketUrl(this.options.location);
    const socket = this.options.createSocket(url);
    this.socket = socket;

    let opened = false;
    let hello: string | null = null;
    let refusal: ConnectionRejection | null = null;

    socket.onopen = () => {
      if (this.socket !== socket) {
        return;
      }
      opened = true;
      if (target.scope === 'device') {
        hello = this.ids();
        socket.send(
          serializeOutgoing({
            kind: 'command',
            type: 'connectDevice',
            requestId: hello,
            payload: { token: target.token, protocolVersion: PROTOCOL_VERSION },
          }),
        );
      } else {
        this.accepted();
      }
    };
    socket.onmessage = (event) => {
      if (this.socket !== socket) {
        return;
      }
      const message = parseIncoming(event.data);
      if (!message) {
        return;
      }
      if (hello !== null && message.kind !== 'event' && message.requestId === hello) {
        // The handshake answer: an ack lets the device in; an error is followed by the agent's close.
        hello = null;
        if (message.kind === 'error') {
          refusal = rejectionFromError(message.code);
        } else {
          this.accepted();
        }
        return;
      }
      this.incoming.next(message);
    };
    socket.onclose = (event) => {
      if (this.socket !== socket) {
        return;
      }
      this.socket = null;
      const rejection = rejectionFromClose(event.code, event.reason) ?? refusal;
      if (rejection) {
        this.reject(rejection);
      } else if (target.scope === 'admin' && !opened && this.options.agentReachable) {
        this.probeAdminRefusal();
      } else {
        this.scheduleRetry();
      }
    };
    // Browsers always follow `error` with `close`; reconnection is handled there.
    socket.onerror = null;
  }

  private accepted(): void {
    this.attempt = 0;
    this.state.set('connected');
  }

  private reject(rejection: ConnectionRejection): void {
    this.disconnect();
    this.rejected.set(rejection);
  }

  /** An admin socket that never opened: a wrong token if the agent is otherwise up, else just retry. */
  private probeAdminRefusal(): void {
    const generation = this.generation;
    const probe = this.options.agentReachable ?? (async () => false);
    this.state.set('reconnecting');
    void probe()
      .catch(() => false)
      .then((reachable) => {
        // Disconnected, retargeted or reopened meanwhile: this answer is stale.
        if (generation !== this.generation) {
          return;
        }
        if (reachable) {
          this.reject('unauthorized');
        } else {
          this.scheduleRetry();
        }
      });
  }

  private scheduleRetry(): void {
    if (!this.running || !this.options.network.isOnline()) {
      return;
    }
    this.state.set('reconnecting');
    const delay = reconnectDelay(this.attempt, this.random, this.backoff);
    this.attempt++;
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      this.open();
    }, delay);
  }

  private onNetworkChange(online: boolean): void {
    if (!this.running) {
      return;
    }
    if (online) {
      this.attempt = 0;
      this.open();
    } else {
      this.cancelRetry();
      this.closeSocket();
      this.state.set('offline');
    }
  }

  private cancelRetry(): void {
    if (this.retryTimer !== null) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
  }

  /** Detaches the current socket before closing it, so its late `close` event cannot schedule a retry. */
  private closeSocket(): void {
    const socket = this.socket;
    if (!socket) {
      return;
    }
    this.socket = null;
    socket.onopen = null;
    socket.onmessage = null;
    socket.onclose = null;
    socket.onerror = null;
    socket.close(NORMAL_CLOSURE);
  }
}
