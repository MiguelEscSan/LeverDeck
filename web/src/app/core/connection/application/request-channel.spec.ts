import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import type { EventMessage } from '../../protocol';
import type { ConnectionRejection } from '../domain/rejection';
import type { ConnectionStatus } from '../domain/connection-status';
import type { ConnectionTarget } from '../domain/connection-target';
import type { IncomingMessage, OutgoingMessage } from '../domain/messages';
import { RequestError } from '../domain/request-error';
import { ConnectionPort } from './connection.port';
import { RequestChannel } from './request-channel';

class FakePort extends ConnectionPort {
  readonly state = signal<ConnectionStatus>('connected');
  readonly status = this.state.asReadonly();
  readonly rejected = signal<ConnectionRejection | null>(null);
  readonly rejection = this.rejected.asReadonly();
  readonly incoming = new Subject<IncomingMessage>();
  readonly messages = this.incoming.asObservable();
  readonly sent: OutgoingMessage[] = [];

  connect(target: ConnectionTarget): void {
    void target;
  }

  disconnect(): void {
    this.state.set('offline');
  }

  send(message: OutgoingMessage): boolean {
    if (this.state() !== 'connected') {
      return false;
    }
    this.sent.push(message);
    return true;
  }
}

describe('RequestChannel', () => {
  let port: FakePort;
  let channel: RequestChannel;
  let next: number;

  beforeEach(() => {
    vi.useFakeTimers();
    next = 0;
    port = new FakePort();
    channel = new RequestChannel(port, { timeoutMs: 1000, ids: () => `r${next++}` });
  });

  afterEach(() => {
    channel.dispose();
    vi.useRealTimers();
  });

  it('sends a command with a fresh request id and resolves with its ack', async () => {
    const result = channel.command('startPairing', {});

    expect(port.sent).toEqual([{ kind: 'command', type: 'startPairing', requestId: 'r0', payload: {} }]);
    port.incoming.next({ kind: 'ack', requestId: 'r0' });

    await expect(result).resolves.toEqual({ kind: 'ack', requestId: 'r0' });
  });

  it('resolves a query with the payload of its result', async () => {
    const result = channel.query('getProfiles', {});
    port.incoming.next({
      kind: 'queryResult',
      type: 'profiles',
      requestId: 'r0',
      payload: { profiles: [{ id: 'p1', name: 'Default', color: 'teal' }] },
    });

    await expect(result).resolves.toEqual({ profiles: [{ id: 'p1', name: 'Default', color: 'teal' }] });
  });

  it('matches answers by request id, whatever the order', async () => {
    const first = channel.command('cancelPairing', {});
    const second = channel.query('getDevices', {});

    port.incoming.next({ kind: 'queryResult', type: 'devices', requestId: 'r1', payload: { devices: [] } });
    port.incoming.next({ kind: 'ack', requestId: 'r0' });

    await expect(second).resolves.toEqual({ devices: [] });
    await expect(first).resolves.toEqual({ kind: 'ack', requestId: 'r0' });
  });

  it('rejects with the wire error code of an error answer', async () => {
    const result = channel.command('executeAction', { profileId: 'p1', pageIndex: 0, buttonId: 'b1' });
    port.incoming.next({ kind: 'error', requestId: 'r0', code: 'actionFailed', message: 'Nope.' });

    await expect(result).rejects.toEqual(new RequestError('actionFailed', 'Nope.'));
  });

  it('delivers events that arrive before the ack of the command that caused them', async () => {
    const events: EventMessage[] = [];
    channel.events.subscribe((event) => events.push(event));
    const result = channel.command('startPairing', {});

    const changed: EventMessage = {
      kind: 'event',
      type: 'pairingSessionChanged',
      payload: { session: null },
    };
    port.incoming.next(changed);
    expect(events).toEqual([changed]);

    port.incoming.next({ kind: 'ack', requestId: 'r0' });
    await expect(result).resolves.toEqual({ kind: 'ack', requestId: 'r0' });
  });

  it('ignores answers to requests it does not know', async () => {
    const result = channel.command('startPairing', {});
    port.incoming.next({ kind: 'ack', requestId: 'someone-else' });
    port.incoming.next({ kind: 'error', requestId: null, code: 'validationFailed', message: 'x' });
    port.incoming.next({ kind: 'ack', requestId: 'r0' });

    await expect(result).resolves.toEqual({ kind: 'ack', requestId: 'r0' });
  });

  it('times out when no answer arrives, and ignores a late one', async () => {
    const result = channel.command('startPairing', {});
    const outcome = expect(result).rejects.toEqual(new RequestError('timeout', 'No answer from the agent in time.'));

    vi.advanceTimersByTime(999);
    port.incoming.next({ kind: 'ack', requestId: 'other' });
    vi.advanceTimersByTime(1);

    await outcome;
    expect(() => port.incoming.next({ kind: 'ack', requestId: 'r0' })).not.toThrow();
  });

  it('rejects at once when the request cannot be sent', async () => {
    port.state.set('reconnecting');

    await expect(channel.command('startPairing', {})).rejects.toEqual(
      new RequestError('notConnected', 'Not connected to the agent.'),
    );
  });

  it('fails every pending request when the connection is lost', async () => {
    const first = channel.command('startPairing', {});
    const second = channel.query('getDevices', {});

    channel.failAll();

    await expect(first).rejects.toHaveProperty('code', 'connectionLost');
    await expect(second).rejects.toHaveProperty('code', 'connectionLost');
  });
});
