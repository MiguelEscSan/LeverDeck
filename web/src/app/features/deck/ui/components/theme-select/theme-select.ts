import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { THEME_LABELS, type ThemePreference } from '../../../../../core/theme/domain/theme';

/**
 * The theme of this phone, at the right of the deck bar: a contrast icon and the current choice. A native select, so
 * the phone shows its own picker, like the profile picker. Presentational.
 */
@Component({
  selector: 'app-theme-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="picker">
      <span class="visually-hidden">Theme</span>
      <svg class="icon" viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="12" r="8" fill="none" stroke="currentColor" stroke-width="2" />
        <path d="M12 4a8 8 0 0 1 0 16z" fill="currentColor" />
      </svg>
      <select [value]="value()" (change)="onChange($event)">
        @for (option of options(); track option) {
          <option [value]="option" [selected]="option === value()">{{ labels[option] }}</option>
        }
      </select>
    </label>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
    }

    .picker {
      display: inline-flex;
      align-items: center;
      gap: var(--ld-space-2);
      min-height: var(--ld-touch-target);
      color: var(--ld-color-muted);
    }

    .icon {
      width: 18px;
      height: 18px;
      flex: none;
      pointer-events: none;
    }

    select {
      appearance: none;
      min-height: var(--ld-touch-target);
      padding: 0;
      border: 0;
      background: transparent;
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-base);
      cursor: pointer;
    }

    option {
      background: var(--ld-color-panel);
      color: var(--ld-color-text);
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
export class ThemeSelect {
  readonly value = input.required<ThemePreference>();
  readonly options = input.required<readonly ThemePreference[]>();
  readonly changed = output<ThemePreference>();

  protected readonly labels = THEME_LABELS;

  protected onChange(event: Event): void {
    this.changed.emit((event.target as HTMLSelectElement).value as ThemePreference);
  }
}
