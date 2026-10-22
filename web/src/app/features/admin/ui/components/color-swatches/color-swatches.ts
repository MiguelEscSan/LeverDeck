import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { KeyColorDto } from '../../../../../core/protocol';
import { KEY_COLOR_NAMES, KEY_COLORS } from '../../../domain/profile-editing';

/** The closed key palette as a radio group of swatches. Presentational. */
@Component({
  selector: 'app-color-swatches',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="swatches" role="radiogroup" [attr.aria-label]="label()">
      @for (color of colors; track color) {
        <button
          type="button"
          role="radio"
          class="swatch"
          [attr.aria-checked]="color === value()"
          [attr.aria-label]="names[color]"
          [attr.title]="names[color]"
          [style.background]="'var(--ld-key-' + color + ')'"
          [disabled]="disabled()"
          (click)="chosen.emit(color)"
        ></button>
      }
    </div>
  `,
  styles: `
    .swatches {
      display: flex;
      flex-wrap: wrap;
      gap: var(--ld-space-2);
    }

    .swatch {
      width: 28px;
      height: 28px;
      padding: 0;
      border: 2px solid var(--ld-key-border);
      border-radius: var(--ld-radius-sm);
      cursor: pointer;

      &[aria-checked='true'] {
        outline: 3px solid var(--ld-color-focus);
        outline-offset: 2px;
      }

      &:disabled {
        cursor: default;
        opacity: 0.55;
      }
    }
  `,
})
export class ColorSwatches {
  readonly value = input.required<KeyColorDto>();
  readonly label = input('Color');
  readonly disabled = input(false);

  readonly chosen = output<KeyColorDto>();

  protected readonly colors = KEY_COLORS;
  protected readonly names = KEY_COLOR_NAMES;
}
