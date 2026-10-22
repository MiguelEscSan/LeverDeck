import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MAX_PAGES } from '../../../domain/profile-editing';

/** "Pages: 1 2 +" under the grid (docs/mocks/desktop.html), plus removing the current page. Presentational. */
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
          (click)="selected.emit(page)"
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

  protected readonly maxPages = MAX_PAGES;
  protected readonly pages = computed(() => Array.from({ length: this.count() }, (_, index) => index));
}
