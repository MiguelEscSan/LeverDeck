import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { ButtonDto } from '../../../../../core/protocol';
import type { DeckCell, KeyFeedback } from '../../../domain/deck';
import { DeckKey } from '../deck-key/deck-key';

/**
 * The page as a regular grid of square keys with even gutters, sized to fit the screen in both directions; empty slots
 * stay as quiet placeholders. Presentational.
 */
@Component({
  selector: 'app-deck-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DeckKey],
  host: { '[style.--columns]': 'columns()', '[style.--rows]': 'rows()' },
  template: `
    <div class="grid" role="group" [attr.aria-label]="label()">
      @for (cell of cells(); track cell.row + ':' + cell.column) {
        @if (cell.button; as button) {
          <app-deck-key [button]="button" [feedback]="feedback()[button.id] ?? null" (pressed)="pressed.emit($event)" />
        } @else {
          <span class="empty" aria-hidden="true"></span>
        }
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      // As wide as the screen allows, but never so wide that the rows overflow the height left for the grid.
      width: min(100%, calc((100dvh - var(--deck-chrome, 220px)) * var(--columns) / var(--rows)));
      margin: 0 auto;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(var(--columns), minmax(0, 1fr));
      gap: clamp(8px, 3.2vmin, 20px);
    }

    .empty {
      aspect-ratio: 1;
      border: 2px dashed var(--ld-color-line);
      border-radius: var(--ld-radius-key);
      opacity: 0.6;
    }
  `,
})
export class DeckGrid {
  readonly cells = input.required<readonly DeckCell[]>();
  readonly columns = input.required<number>();
  readonly rows = input.required<number>();
  readonly feedback = input<Readonly<Record<string, KeyFeedback>>>({});
  readonly label = input('Keys');
  readonly pressed = output<ButtonDto>();
}
