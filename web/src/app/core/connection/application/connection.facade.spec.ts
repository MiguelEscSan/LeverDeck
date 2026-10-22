import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import type { ConnectionStatus } from '../domain/connection-status';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';
import { ConnectionFacade } from './connection.facade';
import { ConnectionPort } from './connection.port';

class FakeConnection extends ConnectionPort {
  readonly state = signal<ConnectionStatus>('offline');
  readonly status = this.state.asReadonly();
  readonly incoming = new Subject<IncomingMessage>();
  readonly messages = this.incoming.asObservable();
  readonly sent: OutgoingMessage[] = [];
  connects = 0;

  connect(): void {
    this.connects++;
  }

  disconnect(): void {
    this.state.set('offline');
  }

  send(message: OutgoingMessage): boolean {
    this.sent.push(message);
    return this.state() === 'connected';
  }
}

describe('ConnectionFacade', () => {
  let port: FakeConnection;
  let facade: ConnectionFacade;

  beforeEach(() => {
    port = new FakeConnection();
    TestBed.configureTestingModule({ providers: [{ provide: ConnectionPort, useValue: port }] });
    facade = TestBed.inject(ConnectionFacade);
  });

  it('mirrors the port status', () => {
    expect(facade.status()).toBe('offline');
    expect(facade.connected()).toBe(false);

    port.state.set('connected');

    expect(facade.status()).toBe('connected');
    expect(facade.connected()).toBe(true);
  });

  it('starts the connection', () => {
    facade.start();

    expect(port.connects).toBe(1);
  });

  it('forwards messages both ways', () => {
    const received: IncomingMessage[] = [];
    facade.messages.subscribe((message) => received.push(message));
    port.state.set('connected');

    port.incoming.next({ kind: 'ack', requestId: 'r1' });
    const sent = facade.send({ kind: 'query', type: 'getSettings', requestId: 'r2', payload: {} });

    expect(received).toEqual([{ kind: 'ack', requestId: 'r1' }]);
    expect(sent).toBe(true);
    expect(port.sent).toHaveLength(1);
  });
});
