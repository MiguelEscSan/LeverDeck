import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { ConnectionStatus } from '../../../core/connection/domain/connection-status';
import type { ConnectionRejection } from '../../../core/connection/domain/rejection';
import { RequestError } from '../../../core/connection/domain/request-error';
import type { DeviceDto, EventMessage, PairingSessionDto } from '../../../core/protocol';
import { AdminFacade } from './admin.facade';

const NOW = Date.parse('2026-10-22T08:30:00.000Z');

const session: PairingSessionDto = {
  code: '472915',
  qrUrl: 'http://leverdeck.local:8765/pair?code=472915',
  fallbackUrl: 'http://192.168.1.20:8765/pair?code=472915',
  expiresAt: '2026-10-22T08:35:00.000Z',
};

const device = (id: string, overrides: Partial<DeviceDto> = {}): DeviceDto => ({
  id,
  name: `Phone ${id}`,
  pairedAt: '2026-10-20T08:00:00.000Z',
  lastSeenAt: '2026-10-22T08:00:00.000Z',
  connected: false,
  revoked: false,
  ...overrides,
});

/** The editor connection as the facade sees it; commands push the events the agent would, before the ack. */
class FakeAdminConnection {
  readonly state = signal<ConnectionStatus>('reconnecting');
  readonly status = this.state.asReadonly();
  readonly connected = computed(() => this.state() === 'connected');
  readonly rejected = signal<ConnectionRejection | null>(null);
  readonly rejection = this.rejected.asReadonly();
  readonly events = new Subject<EventMessage>();
  readonly commands: { type: string; payload: unknown }[] = [];
  token: string | null = 'admin-1';
  current: PairingSessionDto | null = null;
  devices: DeviceDto[] = [device('d1', { connected: true }), device('d2'), device('d3', { revoked: true })];
  fail: RequestError | null = null;
  starts = 0;

  hasToken(): boolean {
    return this.token !== null;
  }

  start(): boolean {
    this.starts++;
    return this.token !== null;
  }

  async query(type: string): Promise<unknown> {
    return type === 'getPairingSession' ? { session: this.current } : { devices: this.devices };
  }

  async command(type: string, payload: unknown): Promise<unknown> {
    this.commands.push({ type, payload });
    if (this.fail) {
      throw this.fail;
    }
    if (type === 'startPairing') {
      this.current = session;
      this.events.next({ kind: 'event', type: 'pairingSessionChanged', payload: { session } });
    }
    if (type === 'cancelPairing') {
      this.current = null;
      this.events.next({ kind: 'event', type: 'pairingSessionChanged', payload: { session: null } });
    }
    return { kind: 'ack', requestId: 'x' };
  }
}

describe('AdminFacade', () => {
  let connection: FakeAdminConnection;
  let facade: AdminFacade;

  const settle = async () => {
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(0);
  };

  const setUp = async (token: string | null = 'admin-1') => {
    vi.useFakeTimers({ now: NOW });
    connection = new FakeAdminConnection();
    connection.token = token;
    TestBed.configureTestingModule({ providers: [{ provide: AdminConnectionFacade, useValue: connection }] });
    facade = TestBed.inject(AdminFacade);
    facade.start();
    connection.state.set('connected');
    await settle();
  };

  afterEach(() => vi.useRealTimers());

  describe('access', () => {
    it('asks for the editor link when the tab has no admin token', async () => {
      await setUp(null);

      expect(facade.access()).toBe('missingToken');
    });

    it('reports a refused admin token and can retry', async () => {
      await setUp();
      connection.rejected.set('unauthorized');

      expect(facade.access()).toBe('rejected');
      facade.retry();
      expect(connection.starts).toBe(2);
    });

    it('is ready once connected', async () => {
      await setUp();

      expect(facade.access()).toBe('ready');
    });
  });

  describe('pairing', () => {
    it('loads the current pairing session and the paired devices once connected', async () => {
      await setUp();

      expect(facade.session()).toBeNull();
      expect(facade.devices().map((item) => item.id)).toEqual(['d1', 'd2']);
    });

    it('starts pairing and shows the session pushed before the ack', async () => {
      await setUp();

      await facade.startPairing();

      expect(connection.commands).toEqual([{ type: 'startPairing', payload: {} }]);
      expect(facade.session()).toEqual(session);
      expect(facade.secondsLeft()).toBe(300);
    });

    it('counts down every second and marks the code expired at zero', async () => {
      await setUp();
      await facade.startPairing();

      await vi.advanceTimersByTimeAsync(28_000);
      expect(facade.secondsLeft()).toBe(272);
      expect(facade.expired()).toBe(false);

      await vi.advanceTimersByTimeAsync(272_000);
      expect(facade.secondsLeft()).toBe(0);
      expect(facade.expired()).toBe(true);
    });

    it('cancels pairing', async () => {
      await setUp();
      await facade.startPairing();

      await facade.cancelPairing();

      expect(connection.commands.at(-1)).toEqual({ type: 'cancelPairing', payload: {} });
      expect(facade.session()).toBeNull();
    });

    it('follows pairingSessionChanged, for example when a phone redeems the code', async () => {
      await setUp();
      await facade.startPairing();

      connection.events.next({ kind: 'event', type: 'pairingSessionChanged', payload: { session: null } });

      expect(facade.session()).toBeNull();
    });

    it('shows a failed command', async () => {
      await setUp();
      connection.fail = new RequestError('timeout');

      await facade.startPairing();

      expect(facade.error()).toMatch(/Try again/);
      expect(facade.busy()).toBe(false);
    });
  });

  describe('devices', () => {
    it('follows devicesChanged', async () => {
      await setUp();

      connection.events.next({
        kind: 'event',
        type: 'devicesChanged',
        payload: { devices: [device('d9', { connected: true })] },
      });

      expect(facade.devices().map((item) => item.id)).toEqual(['d9']);
    });

    it('revokes a device', async () => {
      await setUp();

      await facade.revoke('d2');

      expect(connection.commands).toEqual([{ type: 'revokeDevice', payload: { deviceId: 'd2' } }]);
    });
  });
});
