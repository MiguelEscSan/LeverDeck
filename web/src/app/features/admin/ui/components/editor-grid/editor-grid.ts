import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { ButtonDto } from '../../../../../core/protocol';
import { ACTION_TYPE_NAMES } from '../../../domain/action-catalog';
import { KeyFace } from '../key-face/key-face';

/** A grid position as the editor shows it. */
export interface GridCell {
  readonly row: number;
  readonly column: number;
  readonly button: ButtonDto | null;
}

export interface GridPosition {
  readonly row: number;
  readonly column: number;
}

/**
 * The page being edited (docs/mocks/desktop.html): every position is a control; a key shows its face, an empty
 * position a dashed "+". Choosing one selects it for the inspector. Presentational.
 */
@Component({
  selector: 'app-editor-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KeyFace],
  template: `
    <div class="grid" role="group" [attr.aria-label]="label()" [style.--columns]="columns()">
      @for (cell of cells(); track cell.row + ':' + cell.column) {
        <button
          type="button"
          class="cell"
          [class.empty]="!cell.button"
          [attr.aria-pressed]="isSelected(cell)"
          [attr.aria-label]="describe(cell)"
          (click)="selected.emit({ row: cell.row, column: cell.column })"
        >
          @if (cell.button; as button) {
            <app-key-face [button]="button" />
          } @else {
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 5v14M5 12h14" fill="none" stroke="currentColor" stroke-width="2" />
            </svg>
          }
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(var(--columns), minmax(0, 1fr));
      gap: clamp(8px, 1.6vw, 20px);
      max-width: calc(var(--columns) * 124px);
      margin: 0 auto;
    }

    .cell {
      display: grid;
      // One full-size track: the key face is size-contained (container queries), so it has no width of its own.
      grid-template-columns: 100%;
      place-items: center;
      aspect-ratio: 1;
      padding: 0;
      border: 0;
      border-radius: var(--ld-radius-key);
      background: none;
      color: var(--ld-color-muted);
      cursor: pointer;

      &[aria-pressed='true'] {
        outline: 3px solid var(--ld-color-focus);
        outline-offset: 4px;
      }

      &:hover app-key-face {
        filter: brightness(1.08);
      }
    }

    .empty {
      border: 2px dashed var(--ld-color-field-border);

      svg {
        width: 30%;
        height: 30%;
      }

      &:hover {
        color: var(--ld-color-text);
        border-color: var(--ld-color-muted);
      }
    }
  `,
})
export class EditorGrid {
  readonly cells = input.required<readonly GridCell[]>();
  readonly columns = input.required<number>();
  readonly selection = input<GridPosition | null>(null);
  readonly label = input('Keys');

  readonly selected = output<GridPosition>();

  protected isSelected(cell: GridCell): boolean {
    const selection = this.selection();
    return selection !== null && selection.row === cell.row && selection.column === cell.column;
  }

  protected describe(cell: GridCell): string {
    const position = `row ${cell.row + 1}, column ${cell.column + 1}`;
    if (!cell.button) {
      return `Empty, ${position}`;
    }
    const name = cell.button.label.trim() || ACTION_TYPE_NAMES[cell.button.action.type];
    return `${name}, ${position}`;
  }
}
