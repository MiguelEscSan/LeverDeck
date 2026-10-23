import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { THEME_LABELS, type ThemePreference } from '../../../../../core/theme/domain/theme';

/** System / Dark / Light for this browser, in the sidebar footer: a radio group drawn as segments. Presentational. */
@Component({
  selector: 'app-theme-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="switch">
      <legend class="legend">Theme</legend>
      <div class="segments">
        @for (option of options(); track option) {
          <label class="segment" [class.on]="option === value()">
            <input
              type="radio"
              name="theme"
              [value]="option"
              [checked]="option === value()"
              (change)="changed.emit(option)"
            />
            {{ labels[option] }}
          </label>
        }
      </div>
    </fieldset>
  `,
  styles: `
    :host {
      display: block;
    }

    .switch {
      margin: 0;
      padding: 0;
      border: 0;
      min-width: 0;
    }

    .legend {
      padding: 0;
      margin-bottom: var(--ld-space-1);
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-xs);
    }

    .segments {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 2px;
      padding: 2px;
      border-radius: var(--ld-radius-sm);
      background: var(--ld-color-field);
      box-shadow: inset 0 0 0 1px var(--ld-color-field-border);
    }

    .segment {
      position: relative;
      display: grid;
      place-items: center;
      min-height: 32px;
      border-radius: 8px;
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-sm);
      cursor: pointer;
      transition: background-color var(--ld-duration-press) var(--ld-ease-out);

      &.on {
        background: var(--ld-color-panel);
        color: var(--ld-color-text);
        font-weight: var(--ld-font-weight-semibold);
      }

      &:has(:focus-visible) {
        outline: 2px solid var(--ld-color-focus);
        outline-offset: 1px;
      }
    }

    input {
      position: absolute;
      inset: 0;
      margin: 0;
      opacity: 0;
      cursor: pointer;
    }
  `,
})
export class ThemeSwitch {
  readonly value = input.required<ThemePreference>();
  readonly options = input.required<readonly ThemePreference[]>();
  readonly changed = output<ThemePreference>();

  protected readonly labels = THEME_LABELS;
}
