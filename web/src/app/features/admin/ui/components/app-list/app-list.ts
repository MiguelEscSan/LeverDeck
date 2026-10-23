import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal, viewChild } from '@angular/core';
import type { RegisteredAppDto, StoreAppDto } from '../../../../../core/protocol';
import { STORE_APP_PREFIX } from '../../../domain/app-registration';
import { pageOfApps, REGISTERED_APPS_PAGE_SIZE } from '../../../domain/store-apps-page';
import { AddAppDialog, type AppRegistration } from '../add-app-dialog/add-app-dialog';

export type { AppRegistration } from '../add-app-dialog/add-app-dialog';

/** One registered app as a row shows it. */
interface AppRow {
  readonly app: RegisteredAppDto;
  readonly store: boolean;
  /** The path, or a Store app's AppUserModelId without its shell prefix. */
  readonly detail: string;
  readonly initials: string;
  readonly tint: string;
}

/** Tile colors from the key palette, picked by name so an app keeps its color. */
const TINTS = ['var(--ld-key-blue)', 'var(--ld-key-purple)', 'var(--ld-key-teal)', 'var(--ld-key-red)'];

/**
 * "Apps": the programs keys may launch. Only these run: a key names a registered app, never a path, so nothing else
 * can be started from a phone. The registered apps show in pages, searchable by name or path; "Add app" opens the
 * {@link AddAppDialog} (Microsoft Store or an .exe). Removing is two-step. Presentational.
 */
@Component({
  selector: 'app-app-list',
  imports: [AddAppDialog, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (apps().length === 0) {
      <section class="empty" aria-labelledby="apps-empty-title">
        <span class="empty-mark" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linejoin="round"><rect x="4" y="4" width="7" height="7" rx="1.5" /><rect x="13" y="4" width="7"
            height="7" rx="1.5" /><rect x="4" y="13" width="7" height="7" rx="1.5" /><path d="M16.5 13.5v6M13.5 16.5h6" /></svg>
        </span>
        <h2 id="apps-empty-title">No apps yet</h2>
        <p class="hint">Register a program or a Microsoft Store app, then give it to a key to launch it from your phone.</p>
        <ng-container *ngTemplateOutlet="addButton" />
      </section>
    } @else {
      <div class="toolbar">
        <input
          class="ld-field search"
          type="search"
          autocomplete="off"
          placeholder="Search by name or path"
          aria-label="Search apps"
          [value]="query()"
          (input)="onQuery($event)"
        />
        <span class="count">{{ countLabel() }}</span>
        <ng-container *ngTemplateOutlet="addButton" />
      </div>

      <section class="list" aria-label="Registered apps">
        <ul>
          @for (row of rows(); track row.app.appId) {
            <li class="app">
              <span class="tile" aria-hidden="true" [style.background]="row.tint">{{ row.initials }}</span>
              <div class="info">
                <span class="name">{{ row.app.displayName }}</span>
                <span class="path" [attr.title]="row.app.path">{{ row.detail }}</span>
              </div>
              <span class="kind">
                @if (row.store) {
                  <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                    stroke-width="2" stroke-linejoin="round"><path d="M5 8h14l-1 12H6L5 8Z" /><path d="M9 8V6a3 3 0 0 1 6 0v2" /></svg>
                  Microsoft Store
                } @else {
                  <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                    stroke-width="2" stroke-linejoin="round"><rect x="3" y="4" width="18" height="16" rx="2" /><path d="M3 9h18" /></svg>
                  Program
                }
              </span>
              <div class="row-actions">
                @if (confirming() === row.app.appId) {
                  <button
                    class="ld-btn ld-btn--danger"
                    type="button"
                    [disabled]="busy()"
                    [attr.aria-label]="'Confirm removing ' + row.app.displayName"
                    (click)="confirmRemove(row.app.appId)"
                  >
                    Remove?
                  </button>
                  <button class="ld-btn" type="button" (click)="confirming.set(null)">Keep</button>
                } @else {
                  <button
                    class="ld-btn"
                    type="button"
                    [disabled]="busy()"
                    [attr.aria-label]="'Remove ' + row.app.displayName"
                    (click)="confirming.set(row.app.appId)"
                  >
                    Remove
                  </button>
                }
              </div>
            </li>
          }
        </ul>
        @if (total() === 0) {
          <p class="hint none">No apps match “{{ query() }}”.</p>
        } @else {
          <nav class="pager" aria-label="Apps pages">
            <span class="hint range">Showing {{ range().from }}–{{ range().to }} of {{ total() }}</span>
            @if (view().pageCount > 1) {
              <div class="pages">
                <button
                  class="ld-btn step"
                  type="button"
                  aria-label="Previous page"
                  [disabled]="view().page === 0"
                  (click)="goTo(view().page - 1)"
                >
                  <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                    stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 6-6 6 6 6" /></svg>
                </button>
                @for (number of pageNumbers(); track number) {
                  <button
                    class="ld-btn step"
                    type="button"
                    [attr.aria-label]="'Page ' + (number + 1)"
                    [attr.aria-current]="number === view().page ? 'page' : null"
                    (click)="goTo(number)"
                  >
                    {{ number + 1 }}
                  </button>
                }
                <button
                  class="ld-btn step"
                  type="button"
                  aria-label="Next page"
                  [disabled]="view().page >= view().pageCount - 1"
                  (click)="goTo(view().page + 1)"
                >
                  <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                    stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6" /></svg>
                </button>
              </div>
            }
          </nav>
        }
      </section>
    }

    <ng-template #addButton>
      <button class="ld-btn ld-btn--primary add" type="button" (click)="openAdd()">
        <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
          stroke-width="2.2" stroke-linecap="round"><path d="M12 5v14M5 12h14" /></svg>
        Add app
      </button>
    </ng-template>

    <app-add-app-dialog
      [open]="adding()"
      [apps]="apps()"
      [storeApps]="storeApps()"
      [busy]="busy()"
      (dismiss)="adding.set(false)"
      (register)="register.emit($event)"
      (registerStore)="registerStore.emit($event)"
    />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-4);
    }

    .hint {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }

    ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--ld-space-3);
    }

    .search {
      flex: 1 1 260px;
      max-width: 380px;
      min-height: 44px;
      font-size: var(--ld-font-size-md);
    }

    .count {
      margin-right: auto;
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-sm);
    }

    .ld-btn {
      min-height: 44px;
      padding: 0 var(--ld-space-4);
      font-size: var(--ld-font-size-sm);
    }

    .add {
      min-height: 44px;
      font-size: var(--ld-font-size-md);
    }

    .list,
    .empty {
      border-radius: var(--ld-radius-lg);
      background: var(--ld-color-panel);
    }

    .list {
      overflow: hidden;

      ul {
        padding: 0 var(--ld-space-5);
      }
    }

    .app {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--ld-space-3) var(--ld-space-4);
      padding: var(--ld-space-3) 0;
      border-top: 1px solid var(--ld-color-line);

      &:first-child {
        border-top: 0;
      }
    }

    .tile {
      display: grid;
      place-items: center;
      flex: none;
      width: 40px;
      height: 40px;
      border-radius: var(--ld-radius-sm);
      color: var(--ld-key-icon);
      font-size: var(--ld-font-size-md);
      font-weight: var(--ld-font-weight-bold);
    }

    .info {
      flex: 1 1 220px;
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }

    .name,
    .path {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .name {
      font-size: var(--ld-font-size-md);
      font-weight: var(--ld-font-weight-semibold);
    }

    .path {
      color: var(--ld-color-muted-on-panel);
      font-family: 'Cascadia Mono', Consolas, monospace;
      font-size: var(--ld-font-size-xs);
    }

    .kind {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      flex: none;
      padding: var(--ld-space-1) 10px;
      border-radius: var(--ld-radius-pill);
      background: var(--ld-color-background);
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-xs);
    }

    .row-actions {
      display: flex;
      gap: var(--ld-space-2);
      flex: none;
    }

    .none {
      padding: var(--ld-space-8) var(--ld-space-5);
      text-align: center;
    }

    .pager {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--ld-space-3);
      padding: var(--ld-space-3) var(--ld-space-5);
      border-top: 1px solid var(--ld-color-line);
    }

    .pages {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }

    .step {
      min-width: 44px;
      padding: 0 var(--ld-space-2);

      &[aria-current='page'] {
        border-color: var(--ld-color-text);
        background: var(--ld-color-text);
        color: var(--ld-color-panel);
      }
    }

    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--ld-space-3);
      padding: 64px var(--ld-space-6);
      text-align: center;

      h2 {
        font-size: 18px;
        font-weight: var(--ld-font-weight-semibold);
      }

      .hint {
        max-width: 380px;
      }

      .add {
        margin-top: var(--ld-space-2);
      }
    }

    .empty-mark {
      display: grid;
      place-items: center;
      width: 56px;
      height: 56px;
      border-radius: var(--ld-radius-md);
      background: var(--ld-color-background);
      color: var(--ld-color-muted);
    }
  `,
  host: { role: 'region', 'aria-label': 'Apps' },
})
export class AppList {
  readonly apps = input.required<readonly RegisteredAppDto[]>();
  readonly busy = input(false);
  readonly storeApps = input<readonly StoreAppDto[]>([]);

  readonly register = output<AppRegistration>();
  readonly registerStore = output<StoreAppDto>();
  readonly remove = output<string>();

  protected readonly adding = signal(false);
  protected readonly confirming = signal<string | null>(null);
  protected readonly query = signal('');
  protected readonly page = signal(0);

  protected readonly view = computed(() => pageOfApps(this.apps(), this.query(), this.page()));
  protected readonly total = computed(() => this.view().total);
  protected readonly range = computed(() => {
    const view = this.view();
    const from = view.page * REGISTERED_APPS_PAGE_SIZE + 1;
    return { from, to: from + view.items.length - 1 };
  });
  protected readonly pageNumbers = computed(() => Array.from({ length: this.view().pageCount }, (_, index) => index));
  protected readonly countLabel = computed(() => {
    const count = this.apps().length;
    const all = `${count} ${count === 1 ? 'app' : 'apps'}`;
    return this.query().trim() === '' ? all : `${this.total()} of ${all}`;
  });
  protected readonly rows = computed<readonly AppRow[]>(() =>
    this.view().items.map((app) => {
      const store = app.path.startsWith(STORE_APP_PREFIX);
      return {
        app,
        store,
        detail: store ? app.path.slice(STORE_APP_PREFIX.length) : app.path,
        initials: initialsOf(app.displayName),
        tint: tintOf(app.displayName),
      };
    }),
  );

  private readonly dialog = viewChild.required(AddAppDialog);

  /** Clears the form and closes the dialog, after the page saw the app registered. */
  reset(): void {
    this.dialog().reset();
    this.adding.set(false);
  }

  protected openAdd(): void {
    this.confirming.set(null);
    this.adding.set(true);
  }

  protected onQuery(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
    this.goTo(0);
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.confirming.set(null);
  }

  protected confirmRemove(appId: string): void {
    this.confirming.set(null);
    this.remove.emit(appId);
  }
}

/** Up to two letters for an app's tile: the first of two words ("Visual Studio Code" → "VS"), else of one word. */
function initialsOf(name: string): string {
  const words = name.split(/[^\p{L}\p{N}]+/u).filter((word) => word !== '');
  const first = words[0] ?? '?';
  return (words.length > 1 ? first[0] + words[1][0] : first.slice(0, 2)).toUpperCase();
}

function tintOf(name: string): string {
  let sum = 0;
  for (const char of name) {
    sum += char.codePointAt(0) ?? 0;
  }
  return TINTS[sum % TINTS.length];
}
