import type { IncomingMessage } from '../domain/messages';
import type { NetworkStatus, WebSocketLike } from './websocket-connection';
import { WebSocketConnection } from './websocket-connection';

const URL = 'ws://leverdeck.local:8765/ws';

/** A WebSocket the test drives by hand. `close()` fires `onclose` like a real socket eventually does. */
class FakeSocket implements WebSocketLike {
  readyState = 0;
  readonly sent: string[] = [];
  closedByClient = false;
  onopen: ((event: Event) => void) | null = null;
  onclose: ((event: CloseEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;

  constructor(readonly url: string) {}

  send(data: string): void {
    this.sent.push(data);
  }

  close(): void {
    this.closedByClient = true;
    this.drop();
  }

  open(): void {
    this.readyState = 1;
    this.onopen?.(new Event('open'));
  }

  receive(data: unknown): void {
    this.onmessage?.({ data } as MessageEvent);
  }

  /** The server or the network ends the connection; browsers fire `error` first when it never opened. */
  drop(): void {
    this.readyState = 3;
    this.onerror?.(new Event('error'));
    this.onclose?.({ code: 1006 } as CloseEvent);
  }
}

class FakeNetwork implements NetworkStatus {
  online = true;
  private listener: ((online: boolean) => void) | null = null;

  isOnline(): boolean {
    return this.online;
  }

  watch(onChange: (online: boolean) => void): () => void {
    this.listener = onChange;
    return () => (this.listener = null);
  }

  set(online: boolean): void {
    this.online = online;
    this.listener?.(online);
  }

  get watched(): boolean {
    return this.listener !== null;
  }
}

describe('WebSocketConnection', () => {
  let sockets: FakeSocket[];
  let network: FakeNetwork;
  let connection: WebSocketConnection;

  const latest = () => sockets[sockets.length - 1];

  beforeEach(() => {
    vi.useFakeTimers();
    sockets = [];
    network = new FakeNetwork();
    connection = new WebSocketConnection({
      url: URL,
      createSocket: (url) => {
        const socket = new FakeSocket(url);
        sockets.push(socket);
        return socket;
      },
      network,
      random: () => 1, // no jitter: delays are exactly the exponential sequence
    });
  });

  afterEach(() => {
    connection.disconnect();
    vi.useRealTimers();
  });

  describe('status', () => {
    it('is offline before connecting', () => {
      expect(connection.status()).toBe('offline');
      expect(sockets).toHaveLength(0);
    });

    it('is reconnecting while the first socket opens, then connected', () => {
      connection.connect();
      expect(connection.status()).toBe('reconnecting');
      expect(latest().url).toBe(URL);

      latest().open();

      expect(connection.status()).toBe('connected');
    });

    it('goes back to reconnecting when an open socket drops', () => {
      connection.connect();
      latest().open();

      latest().drop();

      expect(connection.status()).toBe('reconnecting');
    });

    it('is offline after disconnect and does not reconnect', () => {
      connection.connect();
      latest().open();

      connection.disconnect();
      vi.advanceTimersByTime(60_000);

      expect(connection.status()).toBe('offline');
      expect(latest().closedByClient).toBe(true);
      expect(sockets).toHaveLength(1);
      expect(network.watched).toBe(false);
    });

    it('ignores a second connect while already connecting', () => {
      connection.connect();
      connection.connect();

      expect(sockets).toHaveLength(1);
    });
  });

  describe('reconnection', () => {
    it('opens a new socket after the backoff delay', () => {
      connection.connect();
      latest().open();
      latest().drop();

      vi.advanceTimersByTime(499);
      expect(sockets).toHaveLength(1);

      vi.advanceTimersByTime(1);
      expect(sockets).toHaveLength(2);

      latest().open();
      expect(connection.status()).toBe('connected');
    });

    it('backs off exponentially up to 30 seconds while the agent is unreachable', () => {
      connection.connect();
      const delays: number[] = [];

      for (let attempt = 0; attempt < 9; attempt++) {
        const before = sockets.length;
        latest().drop();
        let waited = 0;
        while (sockets.length === before) {
          vi.advanceTimersByTime(100);
          waited += 100;
        }
        delays.push(waited);
      }

      expect(delays).toEqual([500, 1000, 2000, 4000, 8000, 16000, 30000, 30000, 30000]);
      expect(connection.status()).toBe('reconnecting');
    });

    it('starts the backoff again after a successful connection', () => {
      connection.connect();
      latest().drop();
      vi.advanceTimersByTime(500);
      latest().drop();
      vi.advanceTimersByTime(1000);
      latest().open();

      latest().drop();
      vi.advanceTimersByTime(500);

      expect(sockets).toHaveLength(4);
    });

    it('goes offline while the device has no network and retries at once when it returns', () => {
      connection.connect();
      latest().open();

      network.set(false);
      expect(connection.status()).toBe('offline');
      vi.advanceTimersByTime(60_000);
      expect(sockets).toHaveLength(1);

      network.set(true);
      expect(connection.status()).toBe('reconnecting');
      expect(sockets).toHaveLength(2);
    });

    it('starts offline without opening a socket when the device has no network', () => {
      network.online = false;

      connection.connect();

      expect(connection.status()).toBe('offline');
      expect(sockets).toHaveLength(0);
    });

    it('ignores events from a socket it already replaced', () => {
      connection.connect();
      const first = latest();
      first.drop();
      vi.advanceTimersByTime(500);
      latest().open();

      first.onclose?.({ code: 1006 } as CloseEvent);

      expect(connection.status()).toBe('connected');
    });
  });

  describe('messages', () => {
    let received: IncomingMessage[];

    beforeEach(() => {
      received = [];
      connection.messages.subscribe((message) => received.push(message));
      connection.connect();
      latest().open();
    });

    it('parses incoming frames into protocol messages', () => {
      latest().receive('{"kind":"event","type":"profileDeleted","payload":{"profileId":"p1"}}');

      expect(received).toEqual([{ kind: 'event', type: 'profileDeleted', payload: { profileId: 'p1' } }]);
    });

    it('ignores invalid JSON without crashing or dropping the connection', () => {
      latest().receive('{oops');
      latest().receive('{"kind":"ack","requestId":"r1"}');

      expect(received).toEqual([{ kind: 'ack', requestId: 'r1' }]);
      expect(connection.status()).toBe('connected');
    });

    it('sends protocol messages in the wire format while connected', () => {
      const sent = connection.send({ kind: 'query', type: 'getProfiles', requestId: 'r1', payload: {} });

      expect(sent).toBe(true);
      expect(latest().sent).toEqual(['{"kind":"query","type":"getProfiles","requestId":"r1","payload":{}}']);
    });

    it('refuses to send while not connected', () => {
      latest().drop();

      const sent = connection.send({ kind: 'query', type: 'getProfiles', requestId: 'r1', payload: {} });

      expect(sent).toBe(false);
    });
  });
});
