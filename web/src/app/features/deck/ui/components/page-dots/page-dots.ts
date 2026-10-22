import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/** The "Pages: 1 2 3" pill under the grid; the active page is a light filled circle. Presentational. */
@Component({
  selector: 'app-page-dots',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="pages" aria-label="Pages">
      <span class="title" aria-hidden="true">Pages:</span>
      @for (page of pages(); track page) {
        <button
          type="button"
          class="dot"
          [class.on]="page === current()"
          [attr.aria-current]="page === current() ? 'page' : null"
          [attr.aria-label]="'Page ' + (page + 1)"
          (click)="selected.emit(page)"
        >
          {{ page + 1 }}
        </button>
      }
    </nav>
  `,
  styles: `
    :host {
      display: flex;
      justify-content: center;
    }

    .pages {
      display: flex;
      align-items: center;
      gap: var(--ld-space-1);
      padding: var(--ld-space-1) var(--ld-space-3);
      border-radius: var(--ld-radius-pill);
      background: var(--ld-color-panel);
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }

    .title {
      margin-right: var(--ld-space-1);
    }

    .dot {
      display: grid;
      place-items: center;
      width: var(--ld-touch-target);
      height: var(--ld-touch-target);
      padding: 0;
      border: 0;
      border-radius: 50%;
      background: transparent;
      color: var(--ld-color-text);
      font: inherit;
      font-weight: var(--ld-font-weight-semibold);
      cursor: pointer;
    }

    .dot.on {
      background: radial-gradient(circle, var(--ld-color-text) 0 15px, transparent 15.5px);
      color: var(--ld-color-panel);
    }
  `,
})
export class PageDots {
  readonly count = input.required<number>();
  readonly current = input.required<number>();
  readonly selected = output<number>();

  protected readonly pages = computed(() => Array.from({ length: this.count() }, (_, page) => page));
}
