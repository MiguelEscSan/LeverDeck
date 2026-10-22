import { computed, DestroyRef, effect, inject, Injectable, untracked } from '@angular/core';
import type { AckMessage } from '../../protocol';
import { DeviceSession } from '../../auth/application/device-session';
import { AdminTokenStore } from '../../auth/application/token-stores';
import type { OutgoingMessage } from '../domain/messages';
import { ADMIN_CONNECTION, ConnectionPort } from './connection.port';
import {
  type CommandPayload,
  type CommandType,
  type QueryAnswer,
  type QueryPayload,
  type QueryType,
  RequestChannel,
} from './request-channel';

/** What both facades share: status, rejection, events and correlated requests over one port. */
abstract class AgentConnection {
  protected abstract readonly port: ConnectionPort;
  private channelInstance: RequestChannel | null = null;

  protected get channel(): RequestChannel {
    this.channelInstance ??= new RequestChannel(this.port);
    return this.channelInstance;
  }

  get status() {
    return this.port.status;
  }

  get rejection() {
    return this.port.rejection;
  }

  readonly connected = computed(() => this.status() === 'connected');

  /** Agent-pushed events, in arrival order (they may precede the ack of the command that caused them). */
  get events() {
    return this.channel.events;
  }

  /** Every valid message from the agent. */
  get messages() {
    return this.port.messages;
  }

  protected watchConnection(): void {
    // A dropped connection never answers; fail pending requests at once instead of waiting for their timeout.
    effect(() => {
      if (!this.connected()) {
        untracked(() => this.channel.failAll());
      }
    });
    inject(DestroyRef).onDestroy(() => this.channelInstance?.dispose());
  }

  command<T extends CommandType>(type: T, payload: CommandPayload<T>): Promise<AckMessage> {
    return this.channel.command(type, payload);
  }

  query<T extends QueryType>(type: T, payload: QueryPayload<T>): Promise<QueryAnswer<T>> {
    return this.channel.query(type, payload);
  }

  /** Sends a raw message without waiting for an answer. */
  send(message: OutgoingMessage): boolean {
    return this.port.send(message);
  }

  stop(): void {
    this.port.disconnect();
  }
}

/**
 * The device's connection to `/ws`, used by the phone pages. It authenticates with the stored device token, and
 * forgets that token when the agent rejects or revokes it, so the app goes back to pairing instead of retrying.
 * Components never see the socket.
 */
@Injectable({ providedIn: 'root' })
export class ConnectionFacade extends AgentConnection {
  protected readonly port = inject(ConnectionPort);
  private readonly session = inject(DeviceSession);

  readonly paired = this.session.paired;

  constructor() {
    super();
    this.watchConnection();
    effect(() => {
      const rejection = this.rejection();
      if (rejection === 'unauthorized' || rejection === 'deviceRevoked') {
        untracked(() => this.session.forget());
      }
    });
  }

  /** Connects as the paired device; returns `false` (and does nothing) when this browser is not paired. */
  start(): boolean {
    const token = this.session.token();
    if (!token) {
      return false;
    }
    this.port.connect({ scope: 'device', token });
    return true;
  }
}

/** The desktop editor's connection to `/admin/ws`, authenticated with the admin token from the editor link. */
@Injectable({ providedIn: 'root' })
export class AdminConnectionFacade extends AgentConnection {
  protected readonly port = inject(ADMIN_CONNECTION);
  private readonly tokens = inject(AdminTokenStore);

  constructor() {
    super();
    this.watchConnection();
  }

  hasToken(): boolean {
    return this.tokens.read() !== null;
  }

  /** Connects as the admin; returns `false` when the tab has no admin token. */
  start(): boolean {
    const token = this.tokens.read();
    if (!token) {
      return false;
    }
    this.port.connect({ scope: 'admin', token });
    return true;
  }
}
