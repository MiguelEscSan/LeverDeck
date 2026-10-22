import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { ConnectionStatus } from '../../../../../core/connection/domain/connection-status';

const LABELS: Record<ConnectionStatus, string> = {
  connected: 'Connected',
  reconnecting: 'Reconnecting…',
  offline: 'Offline',
};

/** A small pill with a colored dot and the connection state. Presentational: input only. */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="badge" role="status" aria-live="polite" [attr.data-status]="status()">
      <span class="dot" aria-hidden="true"></span>
      {{ label() }}
    </span>
  `,
  styles: `
    :host {
      display: inline-flex;
    }

    .badge {
      display: inline-flex;
      align-items: center;
      gap: var(--ld-space-2);
      min-height: 32px;
      padding: var(--ld-space-1) var(--ld-space-3);
      border-radius: var(--ld-radius-pill);
      border: 1px solid var(--ld-color-line);
      background: var(--ld-color-panel);
      color: var(--ld-color-text);
      font-size: var(--ld-font-size-sm);
      font-weight: var(--ld-font-weight-semibold);
      white-space: nowrap;
    }

    .dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: var(--ld-color-idle);
    }

    [data-status='connected'] .dot {
      background: var(--ld-color-success);
      box-shadow: 0 0 0 4px color-mix(in srgb, var(--ld-color-success) 20%, transparent);
    }

    [data-status='reconnecting'] .dot {
      background: var(--ld-color-warning);
      animation: pulse 1.4s ease-in-out infinite;
    }

    @keyframes pulse {
      50% {
        opacity: 0.35;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      [data-status='reconnecting'] .dot {
        animation: none;
      }
    }
  `,
})
export class StatusBadge {
  readonly status = input.required<ConnectionStatus>();

  protected readonly label = computed(() => LABELS[this.status()]);
}
