import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MAX_PAGES } from '../../../domain/profile-editing';
import { KEY_DRAG_TYPE } from '../editor-grid/editor-grid';

/** A key dropped on a page number, to move it to that page. */
export interface KeyToPage {
  readonly buttonId: string;
  readonly pageIndex: number;
}

/**
 * "Pages: 1 2 +" under the grid (docs/mocks/desktop.html), plus removing the current page. A key dragged from the
 * grid onto a page number moves to that page. Presentational.
 */
@Component({
  selector: 'app-page-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="pages" aria-label="Pages">
      <span class="caption" aria-hidden="true">Pages:</span>
      @for (page of pages(); track page) {
        <button
          type="button"
          class="page"
          [attr.aria-current]="page === current() ? 'page' : null"
          [attr.aria-label]="'Page ' + (page + 1)"
          [class.drop-target]="page === over()"
          (click)="selected.emit(page)"
          (dragover)="onDragOver(page, $event)"
          (dragleave)="over.set(null)"
          (drop)="onDrop(page, $event)"
        >
          {{ page + 1 }}
        </button>
      }
      <button type="button" class="page" aria-label="Add a page" [disabled]="count() >= maxPages" (click)="add.emit()">
        +
      </button>
    </nav>
    <button type="button" class="remove" [disabled]="count() <= 1" (click)="remove.emit()">
      Remove page {{ current() + 1 }}
    </button>
  `,
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: center;
      gap: var(--ld-space-3);
    }

    .pages {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 6px 10px;
      border-radius: var(--ld-radius-pill);
      background: var(--ld-color-panel);
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }

    .page {
      display: grid;
      place-items: center;
      width: 30px;
      height: 30px;
      padding: 0;
      border: 0;
      border-radius: 50%;
      background: none;
      color: var(--ld-color-text);
      font: inherit;
      font-weight: var(--ld-font-weight-semibold);
      cursor: pointer;

      &[aria-current='page'] {
        background: var(--ld-color-text);
        color: var(--ld-color-background);
      }

      &:disabled {
        cursor: default;
        opacity: 0.4;
      }
    }

    .drop-target {
      outline: 3px dashed var(--ld-color-focus);
      outline-offset: 2px;
    }

    .remove {
      border: 0;
      background: none;
      color: var(--ld-color-muted);
      font: inherit;
      font-size: var(--ld-font-size-sm);
      text-decoration: underline;
      cursor: pointer;

      &:disabled {
        visibility: hidden;
      }
    }
  `,
})
export class PageBar {
  readonly count = input.required<number>();
  readonly current = input.required<number>();

  readonly selected = output<number>();
  readonly add = output();
  readonly remove = output();
  readonly keyDropped = output<KeyToPage>();

  protected readonly over = signal<number | null>(null);

  protected onDragOver(page: number, event: DragEvent): void {
    if (page !== this.current() && event.dataTransfer?.types.includes(KEY_DRAG_TYPE)) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'move';
      this.over.set(page);
    }
  }

  protected onDrop(page: number, event: DragEvent): void {
    this.over.set(null);
    const buttonId = event.dataTransfer?.getData(KEY_DRAG_TYPE);
    if (buttonId) {
      event.preventDefault();
      this.keyDropped.emit({ buttonId, pageIndex: page });
    }
  }

  protected readonly maxPages = MAX_PAGES;
  protected readonly pages = computed(() => Array.from({ length: this.count() }, (_, index) => index));
}
