import { computed, inject, Injectable, signal } from '@angular/core';
import { DeviceSession } from '../../../core/auth/application/device-session';
import { PairingApi } from '../../../core/auth/application/pairing-api.port';
import {
  defaultDeviceName,
  isCompleteCode,
  normalizeCode,
  normalizeDeviceName,
  redeemErrorMessage,
} from '../domain/pairing-form';

export interface PairingSetUp {
  /** The code from the QR link (`/pair?code=…`), if any. */
  readonly code: string | null;
  readonly userAgent: string;
}

/**
 * The pairing form: the 6-digit code and the device name, redeemed at the agent for a device token. Provided by the
 * pairing page, so every visit starts clean.
 */
@Injectable()
export class PairingFacade {
  private readonly api = inject(PairingApi);
  private readonly session = inject(DeviceSession);

  private readonly codeState = signal('');
  private readonly nameState = signal('');
  private readonly busyState = signal(false);
  private readonly errorState = signal<string | null>(null);

  readonly code = this.codeState.asReadonly();
  readonly deviceName = this.nameState.asReadonly();
  readonly busy = this.busyState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly canPair = computed(
    () => isCompleteCode(this.codeState()) && normalizeDeviceName(this.nameState()).length > 0,
  );

  setUp({ code, userAgent }: PairingSetUp): void {
    this.codeState.set(normalizeCode(code));
    this.nameState.set(defaultDeviceName(userAgent));
    this.errorState.set(null);
  }

  setCode(raw: string): void {
    this.codeState.set(normalizeCode(raw));
    this.errorState.set(null);
  }

  setDeviceName(name: string): void {
    this.nameState.set(name);
    this.errorState.set(null);
  }

  /** Redeems the code; on success the device token is stored and `true` is returned. */
  async pair(): Promise<boolean> {
    if (this.busyState() || !this.canPair()) {
      return false;
    }
    this.busyState.set(true);
    this.errorState.set(null);
    try {
      const outcome = await this.api.redeem({
        code: this.codeState(),
        deviceName: normalizeDeviceName(this.nameState()),
      });
      if (!outcome.ok) {
        this.errorState.set(redeemErrorMessage(outcome.reason));
        return false;
      }
      this.session.remember(outcome.response.deviceToken);
      return true;
    } finally {
      this.busyState.set(false);
    }
  }
}
