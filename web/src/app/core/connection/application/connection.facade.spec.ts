import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { DeviceTokenStore } from '../../auth/application/token-stores';
import type { ConnectionStatus } from '../domain/connection-status';
import type { ConnectionTarget } from '../domain/connection-target';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';
import type { ConnectionRejection } from '../domain/rejection';
import { AdminTokenStore } from '../../auth/application/token-stores';
import { AdminConnectionFacade, ConnectionFacade } from './connection.facade';
import { ADMIN_CONNECTION, ConnectionPort } from './connection.port';

class FakeConnection extends ConnectionPort {
  readonly state = signal<ConnectionStatus>('offline');
  readonly status = this.state.asReadonly();
  readonly rejected = signal<ConnectionRejection | null>(null);
  readonly rejection = this.rejected.asReadonly();
  readonly incoming = new Subject<IncomingMessage>();
  readonly messages = this.incoming.asObservable();
  readonly sent: OutgoingMessage[] = [];
  readonly targets: ConnectionTarget[] = [];
  disconnects = 0;

  connect(target: ConnectionTarget): void {
    this.targets.push(target);
  }

  disconnect(): void {
    this.disconnects++;
    this.state.set('offline');
  }

  send(message: OutgoingMessage): boolean {
    this.sent.push(message);
    return this.state() === 'connected';
  }
}

class MemoryStore extends DeviceTokenStore {
  constructor(public value: string | null = null) {
    super();
  }

  read(): string | null {
    return this.value;
  }

  write(token: string): void {
    this.value = token;
  }

  clear(): void {
    this.value = null;
  }
}

describe('ConnectionFacade', () => {
  let port: FakeConnection;
  let tokens: MemoryStore;
  let facade: ConnectionFacade;

  const setUp = (token: string | null) => {
    port = new FakeConnection();
    tokens = new MemoryStore(token);
    TestBed.configureTestingModule({
      providers: [
        { provide: ConnectionPort, useValue: port },
        { provide: DeviceTokenStore, useValue: tokens },
      ],
    });
    facade = TestBed.inject(ConnectionFacade);
  };

  it('mirrors the port status', () => {
    setUp('t1');
    expect(facade.status()).toBe('offline');
    expect(facade.connected()).toBe(false);

    port.state.set('connected');

    expect(facade.status()).toBe('connected');
    expect(facade.connected()).toBe(true);
  });

  it('connects as the paired device', () => {
    setUp('t1');

    expect(facade.start()).toBe(true);

    expect(port.targets).toEqual([{ scope: 'device', token: 't1' }]);
  });

  it('does not connect without a device token', () => {
    setUp(null);

    expect(facade.start()).toBe(false);

    expect(port.targets).toEqual([]);
  });

  it.each(['unauthorized', 'deviceRevoked'] as const)(
    'forgets the device token when the agent answers %s',
    (rejection) => {
      setUp('t1');
      facade.start();

      port.rejected.set(rejection);
      TestBed.tick();

      expect(tokens.value).toBeNull();
      expect(facade.paired()).toBe(false);
      expect(facade.rejection()).toBe(rejection);
    },
  );

  it('keeps the token on a protocol mismatch: updating the app fixes it', () => {
    setUp('t1');
    facade.start();

    port.rejected.set('protocolMismatch');
    TestBed.tick();

    expect(tokens.value).toBe('t1');
  });

  it('sends correlated commands once connected', async () => {
    setUp('t1');
    port.state.set('connected');

    const ack = facade.command('executeAction', { profileId: 'p1', pageIndex: 0, buttonId: 'b1' });
    const sent = port.sent[0];
    port.incoming.next({ kind: 'ack', requestId: sent.requestId });

    await expect(ack).resolves.toEqual({ kind: 'ack', requestId: sent.requestId });
    expect(sent).toMatchObject({ kind: 'command', type: 'executeAction' });
  });

  it('fails pending requests when the connection drops', async () => {
    setUp('t1');
    port.state.set('connected');
    TestBed.tick();
    const answer = facade.query('getProfiles', {});

    port.state.set('reconnecting');
    TestBed.tick();

    await expect(answer).rejects.toHaveProperty('code', 'connectionLost');
  });

  it('exposes only events on the event stream', () => {
    setUp('t1');
    const events: IncomingMessage[] = [];
    facade.events.subscribe((event) => events.push(event));

    port.incoming.next({ kind: 'ack', requestId: 'x' });
    port.incoming.next({ kind: 'event', type: 'profileDeleted', payload: { profileId: 'p1' } });

    expect(events).toEqual([{ kind: 'event', type: 'profileDeleted', payload: { profileId: 'p1' } }]);
  });
});

describe('AdminConnectionFacade', () => {
  let port: FakeConnection;
  let tokens: MemoryStore;

  const setUp = (token: string | null) => {
    port = new FakeConnection();
    tokens = new MemoryStore(token);
    TestBed.configureTestingModule({
      providers: [
        { provide: ADMIN_CONNECTION, useValue: port },
        { provide: AdminTokenStore, useValue: tokens },
      ],
    });
    return TestBed.inject(AdminConnectionFacade);
  };

  it('connects with the admin token', () => {
    const facade = setUp('admin-1');

    expect(facade.hasToken()).toBe(true);
    expect(facade.start()).toBe(true);

    expect(port.targets).toEqual([{ scope: 'admin', token: 'admin-1' }]);
  });

  it('does not connect without an admin token', () => {
    const facade = setUp(null);

    expect(facade.hasToken()).toBe(false);
    expect(facade.start()).toBe(false);
    expect(port.targets).toEqual([]);
  });
});
