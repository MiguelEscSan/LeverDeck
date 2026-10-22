import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
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

/** A key dropped (or moved with Alt + an arrow key) onto a position. */
export interface KeyMove extends GridPosition {
  readonly buttonId: string;
}

/** The drag data type of a key, so only LeverDeck keys can be dropped on the grid or the pages. */
export const KEY_DRAG_TYPE = 'application/x-leverdeck-key';

const ARROWS: Readonly<Record<string, readonly [number, number]>> = {
  ArrowUp: [-1, 0],
  ArrowDown: [1, 0],
  ArrowLeft: [0, -1],
  ArrowRight: [0, 1],
};

/**
 * The page being edited (docs/mocks/desktop.html): every position is a control; a key shows its face, an empty
 * position a dashed "+". Choosing one selects it for the inspector. A key can be dragged onto another position (a key
 * there trades places), or moved with Alt + an arrow key. Presentational.
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
          [class.drop-target]="isDropTarget(cell)"
          [class.dragging]="cell.button !== null && cell.button.id === dragging()"
          [attr.draggable]="cell.button ? 'true' : null"
          [attr.aria-pressed]="isSelected(cell)"
          [attr.aria-label]="describe(cell)"
          [attr.aria-keyshortcuts]="cell.button ? 'Alt+ArrowUp Alt+ArrowDown Alt+ArrowLeft Alt+ArrowRight' : null"
          (click)="selected.emit({ row: cell.row, column: cell.column })"
          (keydown)="onKeydown(cell, $event)"
          (dragstart)="onDragStart(cell, $event)"
          (dragend)="onDragEnd()"
          (dragover)="onDragOver(cell, $event)"
          (dragleave)="onDragLeave(cell)"
          (drop)="onDrop(cell, $event)"
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

      &[draggable='true'] {
        cursor: grab;
      }
    }

    .dragging {
      opacity: 0.4;
    }

    .drop-target {
      outline: 3px dashed var(--ld-color-focus);
      outline-offset: 4px;
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
  readonly moved = output<KeyMove>();

  /** The key being dragged from this grid, and the position under it. */
  protected readonly dragging = signal<string | null>(null);
  private readonly over = signal<GridPosition | null>(null);

  protected isDropTarget(cell: GridCell): boolean {
    const over = this.over();
    return over !== null && over.row === cell.row && over.column === cell.column;
  }

  protected onDragStart(cell: GridCell, event: DragEvent): void {
    if (!cell.button || !event.dataTransfer) {
      return;
    }
    event.dataTransfer.setData(KEY_DRAG_TYPE, cell.button.id);
    event.dataTransfer.effectAllowed = 'move';
    this.dragging.set(cell.button.id);
  }

  protected onDragEnd(): void {
    this.dragging.set(null);
    this.over.set(null);
  }

  protected onDragOver(cell: GridCell, event: DragEvent): void {
    if (event.dataTransfer?.types.includes(KEY_DRAG_TYPE)) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'move';
      this.over.set({ row: cell.row, column: cell.column });
    }
  }

  protected onDragLeave(cell: GridCell): void {
    if (this.isDropTarget(cell)) {
      this.over.set(null);
    }
  }

  protected onDrop(cell: GridCell, event: DragEvent): void {
    const buttonId = event.dataTransfer?.getData(KEY_DRAG_TYPE);
    this.onDragEnd();
    if (buttonId) {
      event.preventDefault();
      this.moved.emit({ buttonId, row: cell.row, column: cell.column });
    }
  }

  protected onKeydown(cell: GridCell, event: KeyboardEvent): void {
    const step = ARROWS[event.key];
    if (!cell.button || !event.altKey || !step) {
      return;
    }
    event.preventDefault();
    const rows = Math.ceil(this.cells().length / this.columns());
    const row = cell.row + step[0];
    const column = cell.column + step[1];
    if (row >= 0 && column >= 0 && row < rows && column < this.columns()) {
      this.moved.emit({ buttonId: cell.button.id, row, column });
    }
  }

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
