import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { DeviceDto, EventMessage, PairingSessionDto } from '../../../core/protocol';
import { secondsLeft } from '../domain/admin-time';

/**
 * Whether the editor can work: `missingToken` (opened without the editor link), `rejected` (the agent refused the
 * token), `connecting`, or `ready`.
 */
export type AdminAccess = 'missingToken' | 'rejected' | 'connecting' | 'ready';

/**
 * The desktop editor (`/admin`) for Phase 6: the pairing session (QR, code, countdown) and the paired devices, kept
 * live by `pairingSessionChanged` and `devicesChanged`. The grid editor arrives in Phase 7.
 */
@Injectable({ providedIn: 'root' })
export class AdminFacade {
  private readonly connection = inject(AdminConnectionFacade);

  private readonly hasToken = signal(false);
  private readonly sessionState = signal<PairingSessionDto | null>(null);
  private readonly devicesState = signal<DeviceDto[]>([]);
  private readonly nowState = signal(Date.now());
  private readonly busyState = signal(false);
  private readonly errorState = signal<string | null>(null);

  readonly status = this.connection.status;
  readonly session = this.sessionState.asReadonly();
  /** Paired devices; revoked ones are history and not shown. */
  readonly devices = computed(() => this.devicesState().filter((device) => !device.revoked));
  readonly now = this.nowState.asReadonly();
  readonly busy = this.busyState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly secondsLeft = computed(() => {
    const session = this.sessionState();
    return session ? secondsLeft(session.expiresAt, this.nowState()) : 0;
  });
  readonly expired = computed(() => this.sessionState() !== null && this.secondsLeft() === 0);
  readonly access = computed<AdminAccess>(() => {
    if (!this.hasToken()) {
      return 'missingToken';
    }
    if (this.connection.rejection() === 'unauthorized') {
      return 'rejected';
    }
    return this.connection.connected() ? 'ready' : 'connecting';
  });

  constructor() {
    effect(() => {
      if (this.connection.connected()) {
        untracked(() => void this.refresh());
      }
    });
    // A one-second clock while a code is on screen, for the countdown and "last seen".
    effect((onCleanup) => {
      if (this.sessionState() === null) {
        return;
      }
      untracked(() => this.nowState.set(Date.now()));
      const timer = setInterval(() => this.nowState.set(Date.now()), 1000);
      onCleanup(() => clearInterval(timer));
    });
    this.connection.events.pipe(takeUntilDestroyed()).subscribe((event) => this.onEvent(event));
  }

  /** Connects with the tab's admin token, if it has one. */
  start(): void {
    this.hasToken.set(this.connection.start());
  }

  retry(): void {
    this.start();
  }

  async startPairing(): Promise<void> {
    await this.run(async () => {
      await this.connection.command('startPairing', {});
      // The session normally arrived as an event before the ack; ask if it did not.
      if (this.sessionState() === null || this.expired()) {
        this.sessionState.set((await this.connection.query('getPairingSession', {})).session);
      }
    }, "Couldn't start pairing. Try again.");
  }

  async cancelPairing(): Promise<void> {
    await this.run(async () => {
      await this.connection.command('cancelPairing', {});
      this.sessionState.set(null);
    }, "Couldn't cancel pairing. Try again.");
  }

  async revoke(deviceId: string): Promise<void> {
    await this.run(
      () => this.connection.command('revokeDevice', { deviceId }),
      "Couldn't revoke the device. Try again.",
    );
  }

  private async refresh(): Promise<void> {
    try {
      const [{ session }, { devices }] = await Promise.all([
        this.connection.query('getPairingSession', {}),
        this.connection.query('getDevices', {}),
      ]);
      this.sessionState.set(session);
      this.devicesState.set(devices);
      this.errorState.set(null);
    } catch {
      this.errorState.set("Couldn't load the pairing state. It loads again when the connection is back.");
    }
  }

  private async run(work: () => Promise<unknown>, failure: string): Promise<void> {
    if (this.busyState()) {
      return;
    }
    this.busyState.set(true);
    this.errorState.set(null);
    try {
      await work();
    } catch {
      this.errorState.set(failure);
    } finally {
      this.busyState.set(false);
    }
  }

  private onEvent(event: EventMessage): void {
    if (event.type === 'pairingSessionChanged') {
      this.sessionState.set(event.payload.session);
    } else if (event.type === 'devicesChanged') {
      this.devicesState.set(event.payload.devices);
    }
  }
}
