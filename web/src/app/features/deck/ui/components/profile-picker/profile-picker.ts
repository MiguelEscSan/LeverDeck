import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { ProfileSummaryDto } from '../../../../../core/protocol';

/**
 * The active profile under the header, with a chevron (docs/VISUAL-REFERENCE.md). A native select, so the phone
 * shows its own picker. Presentational.
 */
@Component({
  selector: 'app-profile-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="picker">
      <span class="visually-hidden">Profile</span>
      <span class="dot" aria-hidden="true" [style.background]="'var(--ld-key-' + (activeColor() ?? 'dark') + ')'"></span>
      <select [value]="activeId() ?? ''" (change)="onChange($event)">
        @for (profile of profiles(); track profile.id) {
          <option [value]="profile.id" [selected]="profile.id === activeId()">{{ profile.name }}</option>
        }
      </select>
      <svg class="chevron" viewBox="0 0 24 24" aria-hidden="true">
        <path d="m6 9 6 6 6-6" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
      </svg>
    </label>
  `,
  styles: `
    :host {
      display: inline-flex;
      min-width: 0;
    }

    .picker {
      position: relative;
      display: inline-flex;
      align-items: center;
      gap: var(--ld-space-2);
      min-height: var(--ld-touch-target);
      min-width: 0;
      color: var(--ld-color-muted);
    }

    .dot {
      width: 10px;
      height: 10px;
      flex: none;
      border-radius: 3px;
      box-shadow: 0 0 0 1px var(--ld-key-border);
    }

    select {
      appearance: none;
      min-width: 0;
      max-width: 60vw;
      min-height: var(--ld-touch-target);
      padding: 0 28px 0 0;
      border: 0;
      background: transparent;
      color: var(--ld-color-text);
      font-weight: var(--ld-font-weight-semibold);
      text-overflow: ellipsis;
      cursor: pointer;
    }

    option {
      background: var(--ld-color-panel);
      color: var(--ld-color-text);
    }

    .chevron {
      position: absolute;
      right: 0;
      width: 20px;
      height: 20px;
      pointer-events: none;
    }

    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
})
export class ProfilePicker {
  readonly profiles = input.required<readonly ProfileSummaryDto[]>();
  readonly activeId = input<string | null>(null);
  readonly activeColor = input<string | null>(null);
  readonly selected = output<string>();

  protected onChange(event: Event): void {
    this.selected.emit((event.target as HTMLSelectElement).value);
  }
}
