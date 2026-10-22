import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { ButtonDto } from '../../../../../core/protocol';
import type { KeyFeedback } from '../../../domain/deck';
import { KeyGlyph } from '../key-glyph/key-glyph';

/**
 * One key (docs/VISUAL-REFERENCE.md, Keys): a rounded square in its saturated color with a large icon and an optional
 * label. It sinks while pressed, then flashes green or red with the agent's answer. Presentational.
 */
@Component({
  selector: 'app-deck-key',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KeyGlyph],
  template: `
    <button
      type="button"
      class="key"
      [attr.data-color]="button().color"
      [attr.data-feedback]="feedback()"
      [attr.aria-label]="name()"
      [attr.aria-busy]="feedback() === 'pending'"
      (click)="pressed.emit(button())"
    >
      <app-key-glyph class="glyph" [iconId]="button().iconId" [label]="button().label" />
      @if (button().label) {
        <span class="label">{{ button().label }}</span>
      }
    </button>
  `,
  styleUrl: './deck-key.scss',
})
export class DeckKey {
  readonly button = input.required<ButtonDto>();
  readonly feedback = input<KeyFeedback | null>(null);
  readonly pressed = output<ButtonDto>();

  protected readonly name = computed(() => this.button().label.trim() || this.button().iconId);
}
