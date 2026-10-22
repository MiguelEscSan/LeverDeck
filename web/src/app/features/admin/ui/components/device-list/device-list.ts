import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import type { DeviceDto } from '../../../../../core/protocol';
import { lastSeenLabel } from '../../../domain/admin-time';

/**
 * "Paired devices" (docs/mocks/desktop.html): each device with its state, and a two-step revoke (the first click asks,
 * the second revokes), since a revoked phone has to pair again. Presentational: inputs and outputs only.
 */
@Component({
  selector: 'app-device-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 id="devices-title">Paired devices</h2>
    @if (devices().length === 0) {
      <p class="empty">No devices yet. Pair a phone or tablet with a code.</p>
    } @else {
      <ul>
        @for (device of devices(); track device.id) {
          <li class="device">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
              <rect x="6" y="2" width="12" height="20" rx="3" />
              <path d="M11 18h2" />
            </svg>
            <div class="info">
              <span class="name">{{ device.name }}</span>
              <span class="state" [attr.data-connected]="device.connected">
                <i aria-hidden="true"></i>{{ device.connected ? 'Connected' : 'Offline' }}
                @if (!device.connected) {
                  <span class="meta">· {{ lastSeen(device) }}</span>
                }
              </span>
            </div>
            @if (confirming() === device.id) {
              <button
                class="ld-btn ld-btn--danger"
                type="button"
                [disabled]="busy()"
                [attr.aria-label]="'Confirm revoking ' + device.name"
                (click)="confirm(device.id)"
              >
                Revoke?
              </button>
              <button class="ld-btn" type="button" (click)="confirming.set(null)">Keep</button>
            } @else {
              <button
                class="ld-btn"
                type="button"
                [disabled]="busy()"
                [attr.aria-label]="'Revoke ' + device.name"
                (click)="confirming.set(device.id)"
              >
                Revoke
              </button>
            }
          </li>
        }
      </ul>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-2);
    }

    h2 {
      font-size: var(--ld-font-size-base);
      font-weight: var(--ld-font-weight-bold);
    }

    .empty {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }

    ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .device {
      display: flex;
      align-items: center;
      gap: var(--ld-space-3);
      padding: var(--ld-space-3) 0;
      border-top: 1px solid var(--ld-color-line);
      font-size: var(--ld-font-size-md);

      &:first-child {
        border-top: 0;
      }
    }

    svg {
      flex: none;
      width: 22px;
      height: 22px;
      color: var(--ld-color-muted-on-panel);
    }

    .info {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .meta,
    .state {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-xs);
    }

    .state {
      display: flex;
      align-items: center;
      gap: 6px;
      white-space: nowrap;

      i {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: var(--ld-color-idle);
      }

      &[data-connected='true'] i {
        background: var(--ld-color-success);
      }
    }

    .ld-btn {
      min-height: 40px;
      padding: 0 var(--ld-space-3);
      font-size: var(--ld-font-size-sm);
    }
  `,
  host: { role: 'region', 'aria-labelledby': 'devices-title' },
})
export class DeviceList {
  readonly devices = input.required<readonly DeviceDto[]>();
  /** The current time in milliseconds, for "Last seen …". */
  readonly now = input.required<number>();
  readonly busy = input(false);

  readonly revoke = output<string>();

  protected readonly confirming = signal<string | null>(null);

  protected lastSeen(device: DeviceDto): string {
    return lastSeenLabel(device.lastSeenAt, this.now());
  }

  protected confirm(deviceId: string): void {
    this.confirming.set(null);
    this.revoke.emit(deviceId);
  }
}
