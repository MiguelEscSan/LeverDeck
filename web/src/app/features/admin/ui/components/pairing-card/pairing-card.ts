import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PairingSessionDto } from '../../../../../core/protocol';
import { formatCountdown } from '../../../domain/admin-time';
import { pairingHost } from '../../../domain/pairing-link';
import { QrCode } from '../qr-code/qr-code';

/**
 * "Pair a device" (docs/mocks/desktop.html): the QR of the pairing link, the one-time code and its countdown while a
 * code is live; a button to get one otherwise. Presentational: inputs and outputs only.
 */
@Component({
  selector: 'app-pairing-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [QrCode],
  template: `
    <h2 id="pairing-title">Pair a device</h2>
    @if (live(); as session) {
      <p class="lead">
        Scan with your phone's camera, or open <strong>{{ host() }}</strong> on it and type the code.
      </p>
      <app-qr-code class="qr" [text]="session.qrUrl" label="Pairing QR code" />
      <p class="code" [attr.aria-label]="'Pairing code ' + spokenCode()">
        @for (digit of digits(); track $index) {
          <span aria-hidden="true">{{ digit }}</span>
        }
      </p>
      <p class="expiry" role="timer" aria-live="off">
        Code expires in <strong>{{ countdown() }}</strong>
      </p>
      @if (session.fallbackUrl; as fallback) {
        <p class="fallback">
          If <strong>{{ host() }}</strong> doesn't open, use <strong class="url">{{ fallback }}</strong>
        </p>
      }
      <button class="ld-btn" type="button" [disabled]="busy()" (click)="cancelPairing.emit()">Cancel</button>
    } @else {
      <p class="lead">
        {{ expired() ? 'The code expired.' : 'Get a one-time code to pair a phone or tablet.' }}
        It works once and expires after a few minutes.
      </p>
      <button class="ld-btn ld-btn--primary" type="button" [disabled]="busy()" (click)="startPairing.emit()">
        {{ expired() ? 'New code' : 'Start pairing' }}
      </button>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-3);
    }

    h2 {
      font-size: var(--ld-font-size-base);
      font-weight: var(--ld-font-weight-bold);
    }

    .lead,
    .expiry,
    .fallback {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }

    strong {
      color: var(--ld-color-text);
      font-weight: var(--ld-font-weight-semibold);
    }

    .url {
      overflow-wrap: anywhere;
    }

    .qr {
      width: 200px;
      height: 200px;
      margin: var(--ld-space-3) auto var(--ld-space-1);
    }

    .code {
      display: flex;
      justify-content: center;
      gap: var(--ld-space-2);

      span {
        display: grid;
        place-items: center;
        width: 36px;
        height: 44px;
        border: 2px solid var(--ld-color-field-border);
        border-radius: var(--ld-radius-sm);
        background: var(--ld-color-field);
        font-size: 20px;
        font-weight: var(--ld-font-weight-bold);
        font-variant-numeric: tabular-nums;
      }
    }

    .expiry,
    .fallback {
      text-align: center;
    }

    .expiry strong {
      font-variant-numeric: tabular-nums;
    }

    .ld-btn {
      margin-top: var(--ld-space-2);
    }
  `,
  host: { role: 'region', 'aria-labelledby': 'pairing-title' },
})
export class PairingCard {
  readonly session = input.required<PairingSessionDto | null>();
  readonly secondsLeft = input.required<number>();
  readonly expired = input.required<boolean>();
  readonly busy = input(false);

  readonly startPairing = output();
  readonly cancelPairing = output();

  /** The session while its code still works. */
  protected readonly live = computed(() => (this.expired() ? null : this.session()));
  protected readonly digits = computed(() => [...(this.session()?.code ?? '')]);
  protected readonly spokenCode = computed(() => this.digits().join(' '));
  protected readonly countdown = computed(() => formatCountdown(this.secondsLeft()));
  protected readonly host = computed(() => {
    const session = this.session();
    return (session && pairingHost(session.qrUrl)) ?? 'leverdeck.local';
  });
}
