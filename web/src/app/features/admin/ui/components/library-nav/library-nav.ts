import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** A library section of the editor: the registered apps or the uploaded icons. */
export type LibrarySection = 'apps' | 'icons';

/** "Library" in the sidebar, under the profiles: the apps and icons sections, with how many of each. Presentational. */
@Component({
  selector: 'app-library-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 id="library-title" class="title">Library</h2>
    <nav aria-labelledby="library-title">
      <button
        type="button"
        class="item"
        [attr.aria-current]="current() === 'apps' ? 'page' : null"
        (click)="selected.emit('apps')"
      >
        Apps <span class="count">{{ appCount() }}</span>
      </button>
      <button
        type="button"
        class="item"
        [attr.aria-current]="current() === 'icons' ? 'page' : null"
        (click)="selected.emit('icons')"
      >
        Icons <span class="count">{{ iconCount() }}</span>
      </button>
    </nav>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
    }

    .title {
      padding: 0 var(--ld-space-2) var(--ld-space-3);
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-xs);
      font-weight: var(--ld-font-weight-semibold);
      letter-spacing: 0.8px;
      text-transform: uppercase;
    }

    nav {
      display: flex;
      flex-direction: column;
    }

    .item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      min-height: 42px;
      padding: var(--ld-space-2) var(--ld-space-3);
      border: 0;
      border-radius: var(--ld-radius-sm);
      background: none;
      color: var(--ld-color-text);
      font: inherit;
      font-size: var(--ld-font-size-md);
      text-align: left;
      cursor: pointer;

      &:hover {
        background: color-mix(in srgb, var(--ld-color-panel) 60%, transparent);
      }

      &[aria-current='page'] {
        background: var(--ld-color-panel);
        font-weight: var(--ld-font-weight-semibold);
      }
    }

    .count {
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-xs);
    }
  `,
})
export class LibraryNav {
  readonly current = input<LibrarySection | null>(null);
  readonly appCount = input(0);
  readonly iconCount = input(0);

  readonly selected = output<LibrarySection>();
}
