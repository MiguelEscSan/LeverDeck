import { PROTOCOL_VERSION } from '../../protocol';
import type { ConnectionTarget } from '../domain/connection-target';
import type { IncomingMessage } from '../domain/messages';
import type { NetworkStatus, WebSocketLike } from './websocket-connection';
import { WebSocketConnection } from './websocket-connection';

const URL = 'ws://leverdeck.local:8765/ws';
const DEVICE: ConnectionTarget = { scope: 'device', token: 'device-token' };
const ADMIN: ConnectionTarget = { scope: 'admin', token: 'admin token/+' };

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

  /** The server or the network ends the connection; browsers fire `error` first on an abnormal close. */
  drop(code = 1006, reason = ''): void {
    this.readyState = 3;
    if (code === 1006) {
      this.onerror?.(new Event('error'));
    }
    this.onclose?.({ code, reason } as CloseEvent);
  }

  /** The frames the client sent, parsed. */
  get frames(): Record<string, unknown>[] {
    return this.sent.map((frame) => JSON.parse(frame) as Record<string, unknown>);
  }

  /** Opens the socket and accepts the device's `connectDevice`, as the agent does for a known token. */
  accept(): void {
    this.open();
    const hello = this.frames[0];
    if (hello?.['type'] === 'connectDevice') {
      this.receive(JSON.stringify({ kind: 'ack', requestId: hello['requestId'] }));
    }
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
  let agentReachable: boolean;
  let probes: number;

  const latest = () => sockets[sockets.length - 1];

  beforeEach(() => {
    vi.useFakeTimers();
    sockets = [];
    network = new FakeNetwork();
    agentReachable = true;
    probes = 0;
    connection = new WebSocketConnection({
      location: { protocol: 'http:', host: 'leverdeck.local:8765' },
      createSocket: (url) => {
        const socket = new FakeSocket(url);
        sockets.push(socket);
        return socket;
      },
      network,
      agentReachable: async () => {
        probes++;
        return agentReachable;
      },
      ids: () => `hello-${sockets.length}`,
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
      expect(connection.rejection()).toBeNull();
      expect(sockets).toHaveLength(0);
    });

    it('is reconnecting while the first socket opens and authenticates, then connected', () => {
      connection.connect(DEVICE);
      expect(connection.status()).toBe('reconnecting');
      expect(latest().url).toBe(URL);

      latest().accept();

      expect(connection.status()).toBe('connected');
    });

    it('goes back to reconnecting when an open socket drops', () => {
      connection.connect(DEVICE);
      latest().accept();

      latest().drop();

      expect(connection.status()).toBe('reconnecting');
    });

    it('is offline after disconnect and does not reconnect', () => {
      connection.connect(DEVICE);
      latest().accept();

      connection.disconnect();
      vi.advanceTimersByTime(60_000);

      expect(connection.status()).toBe('offline');
      expect(latest().closedByClient).toBe(true);
      expect(sockets).toHaveLength(1);
      expect(network.watched).toBe(false);
    });

    it('ignores a second connect with the same target while already connecting', () => {
      connection.connect(DEVICE);
      connection.connect({ scope: 'device', token: 'device-token' });

      expect(sockets).toHaveLength(1);
    });

    it('replaces the connection when the target changes', () => {
      connection.connect(DEVICE);
      latest().accept();
      const first = latest();

      connection.connect({ scope: 'device', token: 'new-token' });

      expect(first.closedByClient).toBe(true);
      expect(sockets).toHaveLength(2);
      latest().open();
      expect(latest().frames[0]).toMatchObject({ payload: { token: 'new-token' } });
    });
  });

  describe('device authentication', () => {
    it('sends connectDevice with the token and protocol version as the first message', () => {
      connection.connect(DEVICE);
      latest().open();

      expect(latest().frames).toEqual([
        {
          kind: 'command',
          type: 'connectDevice',
          requestId: 'hello-1',
          payload: { token: 'device-token', protocolVersion: PROTOCOL_VERSION },
        },
      ]);
      expect(connection.status()).toBe('reconnecting');
    });

    it('refuses to send other messages until the agent acknowledges the token', () => {
      connection.connect(DEVICE);
      latest().open();

      expect(connection.send({ kind: 'query', type: 'getProfiles', requestId: 'r1', payload: {} })).toBe(false);
      expect(latest().sent).toHaveLength(1);
    });

    it('keeps the handshake ack to itself', () => {
      const received: IncomingMessage[] = [];
      connection.messages.subscribe((message) => received.push(message));
      connection.connect(DEVICE);

      latest().accept();

      expect(received).toEqual([]);
    });

    it.each(['unauthorized', 'deviceRevoked', 'protocolMismatch'] as const)(
      'stops for good when the agent closes with %s',
      (reason) => {
        connection.connect(DEVICE);
        latest().open();
        latest().receive(
          JSON.stringify({ kind: 'error', requestId: 'hello-1', code: reason, message: 'No.' }),
        );

        latest().drop(1008, reason);
        vi.advanceTimersByTime(60_000);

        expect(connection.rejection()).toBe(reason);
        expect(connection.status()).toBe('offline');
        expect(sockets).toHaveLength(1);
        expect(network.watched).toBe(false);
      },
    );

    it('stops when the handshake error arrived even if the close reason was lost', () => {
      connection.connect(DEVICE);
      latest().open();
      latest().receive(
        JSON.stringify({ kind: 'error', requestId: 'hello-1', code: 'unauthorized', message: 'Unknown token.' }),
      );

      latest().drop();
      vi.advanceTimersByTime(60_000);

      expect(connection.rejection()).toBe('unauthorized');
      expect(sockets).toHaveLength(1);
    });

    it('forwards deviceRevoked and stops when the device is revoked while connected', () => {
      const received: IncomingMessage[] = [];
      connection.messages.subscribe((message) => received.push(message));
      connection.connect(DEVICE);
      latest().accept();

      latest().receive('{"kind":"event","type":"deviceRevoked","payload":{"deviceId":"d1"}}');
      latest().drop(1008, 'deviceRevoked');
      vi.advanceTimersByTime(60_000);

      expect(received).toEqual([{ kind: 'event', type: 'deviceRevoked', payload: { deviceId: 'd1' } }]);
      expect(connection.rejection()).toBe('deviceRevoked');
      expect(sockets).toHaveLength(1);
    });

    it.each([
      [1001, 'shutdown'],
      [1009, 'validationFailed'],
      [1011, 'internal'],
      [1008, 'somethingElse'],
    ])('reconnects after a close with %i %s', (code, reason) => {
      connection.connect(DEVICE);
      latest().accept();

      latest().drop(code, reason);
      vi.advanceTimersByTime(500);

      expect(connection.rejection()).toBeNull();
      expect(sockets).toHaveLength(2);
    });

    it('clears the rejection when connecting again', () => {
      connection.connect(DEVICE);
      latest().open();
      latest().drop(1008, 'unauthorized');

      connection.connect({ scope: 'device', token: 'fresh' });

      expect(connection.rejection()).toBeNull();
      expect(connection.status()).toBe('reconnecting');
      expect(sockets).toHaveLength(2);
    });
  });

  describe('admin authentication', () => {
    it('puts the admin token in the URL and is connected as soon as the socket opens', () => {
      connection.connect(ADMIN);

      expect(latest().url).toBe('ws://leverdeck.local:8765/admin/ws?token=admin%20token%2F%2B');
      latest().open();

      expect(connection.status()).toBe('connected');
      expect(latest().sent).toEqual([]);
    });

    it('treats a socket refused before opening while the agent is up as a wrong token', async () => {
      connection.connect(ADMIN);

      latest().drop();
      await vi.advanceTimersByTimeAsync(60_000);

      expect(probes).toBe(1);
      expect(connection.rejection()).toBe('unauthorized');
      expect(connection.status()).toBe('offline');
      expect(sockets).toHaveLength(1);
    });

    it('keeps retrying while the agent itself is unreachable', async () => {
      agentReachable = false;
      connection.connect(ADMIN);

      latest().drop();
      await vi.advanceTimersByTimeAsync(500);

      expect(connection.rejection()).toBeNull();
      expect(sockets).toHaveLength(2);
    });

    it('retries without probing when an admin socket that was open drops', async () => {
      connection.connect(ADMIN);
      latest().open();

      latest().drop();
      await vi.advanceTimersByTimeAsync(500);

      expect(probes).toBe(0);
      expect(sockets).toHaveLength(2);
    });

    it('ignores a probe answer that arrives after disconnect', async () => {
      connection.connect(ADMIN);
      latest().drop();

      connection.disconnect();
      await vi.advanceTimersByTimeAsync(60_000);

      expect(connection.rejection()).toBeNull();
      expect(sockets).toHaveLength(1);
    });
  });

  describe('reconnection', () => {
    it('opens a new socket after the backoff delay', () => {
      connection.connect(DEVICE);
      latest().accept();
      latest().drop();

      vi.advanceTimersByTime(499);
      expect(sockets).toHaveLength(1);

      vi.advanceTimersByTime(1);
      expect(sockets).toHaveLength(2);

      latest().accept();
      expect(connection.status()).toBe('connected');
    });

    it('backs off exponentially up to 30 seconds while the agent is unreachable', () => {
      connection.connect(DEVICE);
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
      connection.connect(DEVICE);
      latest().drop();
      vi.advanceTimersByTime(500);
      latest().drop();
      vi.advanceTimersByTime(1000);
      latest().accept();

      latest().drop();
      vi.advanceTimersByTime(500);

      expect(sockets).toHaveLength(4);
    });

    it('goes offline while the device has no network and retries at once when it returns', () => {
      connection.connect(DEVICE);
      latest().accept();

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

      connection.connect(DEVICE);

      expect(connection.status()).toBe('offline');
      expect(sockets).toHaveLength(0);
    });

    it('ignores events from a socket it already replaced', () => {
      connection.connect(DEVICE);
      const first = latest();
      first.drop();
      vi.advanceTimersByTime(500);
      latest().accept();

      first.onclose?.({ code: 1008, reason: 'unauthorized' } as CloseEvent);

      expect(connection.status()).toBe('connected');
      expect(connection.rejection()).toBeNull();
    });
  });

  describe('messages', () => {
    let received: IncomingMessage[];

    beforeEach(() => {
      received = [];
      connection.messages.subscribe((message) => received.push(message));
      connection.connect(DEVICE);
      latest().accept();
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
      expect(latest().sent.at(-1)).toBe('{"kind":"query","type":"getProfiles","requestId":"r1","payload":{}}');
    });

    it('refuses to send while not connected', () => {
      latest().drop();

      const sent = connection.send({ kind: 'query', type: 'getProfiles', requestId: 'r1', payload: {} });

      expect(sent).toBe(false);
    });
  });
});
