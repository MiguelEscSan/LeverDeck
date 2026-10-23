import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  type ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { RegisteredAppDto, StoreAppDto } from '../../../../../core/protocol';
import {
  APP_NAME_MAX_LENGTH,
  APP_PATH_MAX_LENGTH,
  appFormError,
  cleanPath,
  nameFromPath,
  storeAppPath,
} from '../../../domain/app-registration';
import { pageOf } from '../../../domain/store-apps-page';

/** A new app to register: its name and the full path of its executable. */
export interface AppRegistration {
  readonly displayName: string;
  readonly path: string;
}

type AddAppTab = 'store' | 'program';

/**
 * "Add app": a modal with two ways to register an app. The Microsoft Store tab lists the Store apps of this PC; the
 * Program tab takes an executable's name and full path (Shift + right-click the .exe, "Copy as path"). The parent
 * decides when it is open: Close, Done, Cancel and Escape ask it to close. Presentational.
 */
@Component({
  selector: 'app-add-app-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog aria-labelledby="add-app-title" (close)="onClosed()">
      <div class="body">
        <header class="head">
          <div class="titles">
            <h2 id="add-app-title">Add app</h2>
            <p class="hint">Pick one of the Microsoft Store apps on this PC, or point to a program’s .exe.</p>
          </div>
          <button class="close" type="button" aria-label="Close" (click)="dismiss.emit()">
            <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              stroke-width="2" stroke-linecap="round"><path d="M6 6l12 12M18 6 6 18" /></svg>
          </button>
        </header>

        <div class="tabs" role="tablist" aria-label="Where the app comes from">
          <button
            id="add-app-tab-store"
            class="tab"
            type="button"
            role="tab"
            aria-controls="add-app-panel-store"
            [attr.aria-selected]="tab() === 'store'"
            [attr.tabindex]="tab() === 'store' ? 0 : -1"
            (click)="tab.set('store')"
            (keydown)="onTabKey($event)"
          >
            <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              stroke-width="2" stroke-linejoin="round"><path d="M5 8h14l-1 12H6L5 8Z" /><path d="M9 8V6a3 3 0 0 1 6 0v2" /></svg>
            Microsoft Store
          </button>
          <button
            id="add-app-tab-program"
            class="tab"
            type="button"
            role="tab"
            aria-controls="add-app-panel-program"
            [attr.aria-selected]="tab() === 'program'"
            [attr.tabindex]="tab() === 'program' ? 0 : -1"
            (click)="tab.set('program')"
            (keydown)="onTabKey($event)"
          >
            <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              stroke-width="2" stroke-linejoin="round"><rect x="3" y="4" width="18" height="16" rx="2" /><path d="M3 9h18" /></svg>
            Program (.exe)
          </button>
        </div>

        @if (tab() === 'store') {
          <div id="add-app-panel-store" class="panel" role="tabpanel" aria-labelledby="add-app-tab-store">
            @if (storeApps().length === 0) {
              <p class="hint">No Microsoft Store apps were found on this PC.</p>
            } @else {
              <input
                class="ld-field"
                type="search"
                autocomplete="off"
                placeholder="Search Store apps"
                aria-label="Search Store apps"
                [value]="storeQuery()"
                (input)="onStoreQuery($event)"
              />
              <ul class="store-list">
                @for (store of storeView().items; track store.appUserModelId) {
                  <li class="store-app">
                    <span class="store-name">{{ store.name }}</span>
                    @if (isRegistered(store)) {
                      <span class="added">
                        <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"
                          stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">
                          <path d="m5 12 5 5 9-10" /></svg>
                        Added
                      </span>
                    } @else {
                      <button
                        class="ld-btn"
                        type="button"
                        [disabled]="busy()"
                        [attr.aria-label]="'Add ' + store.name"
                        (click)="registerStore.emit(store)"
                      >
                        Add
                      </button>
                    }
                  </li>
                } @empty {
                  <li class="hint none">No Store apps match “{{ storeQuery() }}”.</li>
                }
              </ul>
              @if (storeView().pageCount > 1) {
                <nav class="pager" aria-label="Store apps pages">
                  <span class="hint">Page {{ storeView().page + 1 }} of {{ storeView().pageCount }}</span>
                  <div class="pages">
                    <button
                      class="ld-btn step"
                      type="button"
                      aria-label="Previous Store page"
                      [disabled]="storeView().page === 0"
                      (click)="storePage.set(storeView().page - 1)"
                    >
                      <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                        stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 6-6 6 6 6" /></svg>
                    </button>
                    <button
                      class="ld-btn step"
                      type="button"
                      aria-label="Next Store page"
                      [disabled]="storeView().page >= storeView().pageCount - 1"
                      (click)="storePage.set(storeView().page + 1)"
                    >
                      <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                        stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6" /></svg>
                    </button>
                  </div>
                </nav>
              }
            }
            <div class="actions">
              <button class="ld-btn" type="button" (click)="dismiss.emit()">Done</button>
            </div>
          </div>
        } @else {
          <form
            id="add-app-panel-program"
            class="panel form"
            role="tabpanel"
            aria-labelledby="add-app-tab-program"
            novalidate
            (submit)="submit($event)"
          >
            <label class="ld-label" for="app-path">Program path</label>
            <input
              id="app-path"
              class="ld-field path-field"
              type="text"
              autocomplete="off"
              spellcheck="false"
              placeholder="C:\\Program Files\\App\\app.exe"
              [attr.maxlength]="pathMaxLength + 2"
              [value]="path()"
              (input)="onPath($event)"
            />
            <p class="hint tip">Shift + right-click the .exe, choose “Copy as path” and paste it here.</p>
            <label class="ld-label" for="app-name">Name on the keys</label>
            <input
              id="app-name"
              class="ld-field"
              type="text"
              autocomplete="off"
              [attr.maxlength]="nameMaxLength"
              [value]="name()"
              (input)="onName($event)"
            />
            <p class="ld-error form-error" role="alert">{{ formError() ?? '' }}</p>
            <div class="actions">
              <button class="ld-btn" type="button" (click)="dismiss.emit()">Cancel</button>
              <button class="ld-btn ld-btn--primary" type="submit" [disabled]="busy()">Register app</button>
            </div>
          </form>
        }
      </div>
    </dialog>
  `,
  styles: `
    dialog {
      width: min(600px, calc(100vw - 32px));
      max-height: calc(100dvh - 64px);
      padding: 0;
      border: 1px solid var(--ld-color-border);
      border-radius: var(--ld-radius-xl);
      background: var(--ld-color-surface);
      color: var(--ld-color-text);
      box-shadow: var(--ld-shadow-menu);

      &::backdrop {
        background: rgb(0 0 0 / 60%);
      }
    }

    .body {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-4);
      padding: var(--ld-space-6);
    }

    .head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--ld-space-4);
    }

    .titles {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-1);
    }

    h2 {
      font-size: 20px;
      font-weight: var(--ld-font-weight-semibold);
    }

    .hint {
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-sm);
    }

    .close {
      display: grid;
      place-items: center;
      flex: none;
      width: 44px;
      height: 44px;
      border: 0;
      border-radius: var(--ld-radius-sm);
      background: transparent;
      color: var(--ld-color-text);
      cursor: pointer;
    }

    .tabs {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--ld-space-1);
      padding: var(--ld-space-1);
      border-radius: var(--ld-radius-md);
      background: var(--ld-color-field);
    }

    .tab {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--ld-space-2);
      min-height: 44px;
      border: 0;
      border-radius: var(--ld-radius-sm);
      background: transparent;
      color: var(--ld-color-muted);
      font: inherit;
      font-weight: var(--ld-font-weight-semibold);
      cursor: pointer;

      &[aria-selected='true'] {
        background: var(--ld-color-panel);
        color: var(--ld-color-text);
      }
    }

    .panel {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-3);
    }

    ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .store-app {
      display: flex;
      align-items: center;
      gap: var(--ld-space-3);
      min-height: 44px;
      padding: var(--ld-space-2) 0;
      border-top: 1px solid var(--ld-color-line);

      &:first-child {
        border-top: 0;
      }
    }

    .store-name {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-size: var(--ld-font-size-md);
    }

    .added {
      display: inline-flex;
      align-items: center;
      gap: var(--ld-space-1);
      padding: 0 var(--ld-space-3);
      color: var(--ld-color-success);
      font-size: var(--ld-font-size-sm);
      font-weight: var(--ld-font-weight-semibold);
    }

    .none {
      padding: var(--ld-space-5) 0;
      text-align: center;
    }

    .pager {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--ld-space-2);
    }

    .pages {
      display: flex;
      gap: var(--ld-space-2);
    }

    .form {
      gap: var(--ld-space-2);
    }

    .ld-label {
      margin: var(--ld-space-1) 0 0;
    }

    .ld-field {
      min-height: 44px;
      font-size: var(--ld-font-size-md);
    }

    .path-field {
      font-family: 'Cascadia Mono', Consolas, monospace;
      font-size: var(--ld-font-size-sm);
    }

    .tip {
      font-size: var(--ld-font-size-xs);
    }

    .form-error {
      min-height: 1.2em;
      font-size: var(--ld-font-size-sm);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: var(--ld-space-2);
      padding-top: var(--ld-space-1);
    }

    .ld-btn {
      min-height: 44px;
      padding: 0 var(--ld-space-4);
      font-size: var(--ld-font-size-sm);
    }

    .step {
      width: 44px;
      padding: 0;
    }
  `,
})
export class AddAppDialog {
  readonly open = input(false);
  readonly apps = input<readonly RegisteredAppDto[]>([]);
  readonly storeApps = input<readonly StoreAppDto[]>([]);
  readonly busy = input(false);

  readonly register = output<AppRegistration>();
  readonly registerStore = output<StoreAppDto>();
  /** Asks the parent to close the dialog. */
  readonly dismiss = output<void>();

  /** The tab shown; it stays on the last one used while the component lives. */
  protected readonly tab = signal<AddAppTab>('store');

  protected readonly storeQuery = signal('');
  protected readonly storePage = signal(0);
  protected readonly storeView = computed(() => pageOf(this.storeApps(), this.storeQuery(), this.storePage()));

  protected readonly nameMaxLength = APP_NAME_MAX_LENGTH;
  protected readonly pathMaxLength = APP_PATH_MAX_LENGTH;
  protected readonly path = signal('');
  protected readonly name = signal('');
  protected readonly formError = signal<string | null>(null);
  /** Whether the name still follows the path (until the owner types one). */
  private nameFollowsPath = true;

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    afterRenderEffect(() => {
      const element = this.dialog().nativeElement;
      if (this.open()) {
        showModal(element);
      } else {
        closeDialog(element);
      }
    });
  }

  /** Clears the program form, after the page saw the app registered. */
  reset(): void {
    this.path.set('');
    this.name.set('');
    this.formError.set(null);
    this.nameFollowsPath = true;
  }

  protected isRegistered(store: StoreAppDto): boolean {
    const target = storeAppPath(store.appUserModelId);
    return this.apps().some((app) => app.path === target);
  }

  protected onStoreQuery(event: Event): void {
    this.storeQuery.set((event.target as HTMLInputElement).value);
    this.storePage.set(0);
  }

  protected onPath(event: Event): void {
    this.path.set((event.target as HTMLInputElement).value);
    if (this.nameFollowsPath) {
      this.name.set(nameFromPath(this.path()));
    }
  }

  protected onName(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
    this.nameFollowsPath = this.name().trim() === '';
  }

  protected submit(event: Event): void {
    event.preventDefault();
    const error = appFormError(this.name(), this.path());
    this.formError.set(error);
    if (!error) {
      this.register.emit({ displayName: this.name().trim(), path: cleanPath(this.path()) });
    }
  }

  /** Arrow keys, Home and End move between the two tabs, as a tab list does. */
  protected onTabKey(event: KeyboardEvent): void {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
      return;
    }
    event.preventDefault();
    const next: AddAppTab =
      event.key === 'Home' ? 'store' : event.key === 'End' ? 'program' : this.tab() === 'store' ? 'program' : 'store';
    this.tab.set(next);
    (event.currentTarget as HTMLElement).parentElement?.querySelector<HTMLElement>(`#add-app-tab-${next}`)?.focus();
  }

  /** Escape closes a modal dialog by itself: tell the parent, so its state follows. */
  protected onClosed(): void {
    if (this.open()) {
      this.dismiss.emit();
    }
  }
}

// jsdom (the test DOM) has no showModal or close, so the `open` attribute stands in there.
function showModal(dialog: HTMLDialogElement): void {
  if (dialog.hasAttribute('open')) {
    return;
  }
  if (typeof dialog.showModal === 'function') {
    dialog.showModal();
  } else {
    dialog.setAttribute('open', '');
  }
}

function closeDialog(dialog: HTMLDialogElement): void {
  if (!dialog.hasAttribute('open')) {
    return;
  }
  if (typeof dialog.close === 'function') {
    dialog.close();
  } else {
    dialog.removeAttribute('open');
  }
}
