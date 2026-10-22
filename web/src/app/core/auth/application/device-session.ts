import { computed, inject, Injectable, signal } from '@angular/core';
import { DeviceTokenStore } from './token-stores';

/** Whether this browser is paired with the agent, and its device token. */
@Injectable({ providedIn: 'root' })
export class DeviceSession {
  private readonly store = inject(DeviceTokenStore);
  private readonly current = signal(this.store.read());

  readonly token = this.current.asReadonly();
  readonly paired = computed(() => this.current() !== null);

  /** Keeps the token from a successful pairing. */
  remember(token: string): void {
    this.store.write(token);
    this.current.set(token);
  }

  /** Drops the token, for example after the agent rejected or revoked it. */
  forget(): void {
    this.store.clear();
    this.current.set(null);
  }
}
