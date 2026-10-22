import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { ConnectionPort } from '../application/connection.port';
import { type BackoffPolicy, DEFAULT_BACKOFF, reconnectDelay } from '../domain/backoff';
import type { ConnectionStatus } from '../domain/connection-status';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';
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
  readonly url: string;
  readonly createSocket: (url: string) => WebSocketLike;
  readonly network: NetworkStatus;
  /** A value in [0, 1] for the backoff jitter. Defaults to `Math.random`. */
  readonly random?: () => number;
  readonly backoff?: BackoffPolicy;
}

const OPEN = 1;
const NORMAL_CLOSURE = 1000;

/**
 * The WebSocket adapter for {@link ConnectionPort}: one socket at a time, reconnection with exponential backoff and
 * jitter (capped at 30 s by default), and an `offline` pause while the device has no network.
 */
export class WebSocketConnection extends ConnectionPort {
  private readonly state = signal<ConnectionStatus>('offline');
  private readonly incoming = new Subject<IncomingMessage>();
  private readonly random: () => number;
  private readonly backoff: BackoffPolicy;

  private socket: WebSocketLike | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private attempt = 0;
  private running = false;
  private stopWatchingNetwork: (() => void) | null = null;

  readonly status = this.state.asReadonly();
  readonly messages = this.incoming.asObservable();

  constructor(private readonly options: WebSocketConnectionOptions) {
    super();
    this.random = options.random ?? Math.random;
    this.backoff = options.backoff ?? DEFAULT_BACKOFF;
  }

  connect(): void {
    if (this.running) {
      return;
    }
    this.running = true;
    this.attempt = 0;
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
    this.cancelRetry();
    this.closeSocket();
    this.state.set('reconnecting');

    const socket = this.options.createSocket(this.options.url);
    this.socket = socket;

    socket.onopen = () => {
      if (this.socket !== socket) {
        return;
      }
      this.attempt = 0;
      this.state.set('connected');
    };
    socket.onmessage = (event) => {
      if (this.socket !== socket) {
        return;
      }
      const message = parseIncoming(event.data);
      if (message) {
        this.incoming.next(message);
      }
    };
    socket.onclose = () => {
      if (this.socket !== socket) {
        return;
      }
      this.socket = null;
      this.scheduleRetry();
    };
    // Browsers always follow `error` with `close`; reconnection is handled there.
    socket.onerror = null;
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
