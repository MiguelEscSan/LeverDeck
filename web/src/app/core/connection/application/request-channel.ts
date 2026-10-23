import { filter, type Observable, type Subscription } from 'rxjs';
import type {
  AckMessage,
  AgentStatusResult,
  CommandMessage,
  DevicesResult,
  EventMessage,
  IconsResult,
  InstalledGamesResult,
  PairingSessionResult,
  ProfileResult,
  ProfilesResult,
  QueryMessage,
  QueryResultMessage,
  SettingsResult,
  StoreAppsResult,
} from '../../protocol';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';
import { RequestError } from '../domain/request-error';
import { requestIds } from '../domain/request-ids';
import type { ConnectionPort } from './connection.port';

/** The result each query type is answered with (ROADMAP 3.3). */
export interface QueryResults {
  getProfiles: ProfilesResult;
  getProfile: ProfileResult;
  getDevices: DevicesResult;
  getPairingSession: PairingSessionResult;
  getSettings: SettingsResult;
  getAgentStatus: AgentStatusResult;
  getInstalledGames: InstalledGamesResult;
  getStoreApps: StoreAppsResult;
  getIcons: IconsResult;
}

type PayloadOf<Union, Type> = Extract<Union, { type: Type }> extends { payload: infer P } ? P : never;

export type CommandType = Exclude<CommandMessage['type'], 'connectDevice'>;
export type CommandPayload<T extends CommandType> = PayloadOf<CommandMessage, T>;
export type QueryType = QueryMessage['type'];
export type QueryPayload<T extends QueryType> = PayloadOf<QueryMessage, T>;
export type QueryAnswer<T extends QueryType> = QueryResults[T]['payload'];

export interface RequestChannelOptions {
  /** How long an answer may take. Defaults to {@link DEFAULT_REQUEST_TIMEOUT_MS}. */
  readonly timeoutMs?: number;
  /** Request id generator; defaults to {@link requestIds}. */
  readonly ids?: () => string;
}

export const DEFAULT_REQUEST_TIMEOUT_MS = 8000;

interface Pending {
  readonly resolve: (message: AckMessage | QueryResultMessage) => void;
  readonly reject: (error: RequestError) => void;
  readonly timer: ReturnType<typeof setTimeout>;
}

const answerId = (message: IncomingMessage): string | null =>
  message.kind === 'event' ? null : message.requestId;

/**
 * Request/response over a {@link ConnectionPort}: every command or query gets a fresh `requestId`, and the promise
 * settles with the `ack`/`queryResult` (or rejects with the `error`) carrying the same id, or with a timeout. Events
 * have no id and flow through {@link events} whenever they arrive — also before the ack of the command that caused
 * them, as the agent sends them (docs/ARCHITECTURE.md, Event routing).
 */
export class RequestChannel {
  private readonly pending = new Map<string, Pending>();
  private readonly timeoutMs: number;
  private readonly ids: () => string;
  private readonly subscription: Subscription;

  readonly events: Observable<EventMessage>;

  constructor(
    private readonly port: ConnectionPort,
    options: RequestChannelOptions = {},
  ) {
    this.timeoutMs = options.timeoutMs ?? DEFAULT_REQUEST_TIMEOUT_MS;
    this.ids = options.ids ?? requestIds();
    this.events = port.messages.pipe(
      filter((message): message is EventMessage => message.kind === 'event'),
    );
    this.subscription = port.messages.subscribe((message) => this.settle(message));
  }

  /** Sends a command; resolves with its `ack`. */
  command<T extends CommandType>(type: T, payload: CommandPayload<T>): Promise<AckMessage> {
    return this.request(
      (requestId) => ({ kind: 'command', type, requestId, payload }) as CommandMessage,
    ) as Promise<AckMessage>;
  }

  /** Sends a query; resolves with the payload of its result. */
  async query<T extends QueryType>(type: T, payload: QueryPayload<T>): Promise<QueryAnswer<T>> {
    const result = await this.request(
      (requestId) => ({ kind: 'query', type, requestId, payload }) as QueryMessage,
    );
    return (result as QueryResults[T]).payload;
  }

  /** Sends any request built around a fresh id; resolves with the matching answer. */
  request(build: (requestId: string) => OutgoingMessage): Promise<AckMessage | QueryResultMessage> {
    const requestId = this.ids();
    return new Promise((resolve, reject) => {
      if (!this.port.send(build(requestId))) {
        reject(new RequestError('notConnected', 'Not connected to the agent.'));
        return;
      }
      const timer = setTimeout(() => {
        this.pending.delete(requestId);
        reject(new RequestError('timeout', 'No answer from the agent in time.'));
      }, this.timeoutMs);
      this.pending.set(requestId, { resolve, reject, timer });
    });
  }

  /** Rejects every pending request, for example when the connection dropped. */
  failAll(): void {
    const pending = [...this.pending.values()];
    this.pending.clear();
    for (const entry of pending) {
      clearTimeout(entry.timer);
      entry.reject(new RequestError('connectionLost', 'The connection to the agent was lost.'));
    }
  }

  dispose(): void {
    this.subscription.unsubscribe();
    this.failAll();
  }

  private settle(message: IncomingMessage): void {
    const id = answerId(message);
    const entry = id === null ? undefined : this.pending.get(id);
    if (!entry || id === null) {
      return;
    }
    this.pending.delete(id);
    clearTimeout(entry.timer);
    if (message.kind === 'error') {
      entry.reject(new RequestError(message.code, message.message));
    } else if (message.kind !== 'event') {
      entry.resolve(message);
    }
  }
}
