import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import type { RegisteredAppDto } from '../../../../../core/protocol';
import {
  APP_NAME_MAX_LENGTH,
  APP_PATH_MAX_LENGTH,
  appFormError,
  cleanPath,
  nameFromPath,
} from '../../../domain/app-registration';

/** A new app to register: its name and the full path of its executable. */
export interface AppRegistration {
  readonly displayName: string;
  readonly path: string;
}

/**
 * "Apps": the programs keys may launch. Only these run: a key names a registered app, never a path, so nothing else
 * can be started from a phone. Registering takes a name and the executable's full path (Shift + right-click the .exe,
 * "Copy as path"); removing is two-step. Presentational.
 */
@Component({
  selector: 'app-app-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 id="apps-title">Apps</h2>
    @if (apps().length === 0) {
      <p class="hint">No apps yet. Register a program here to launch it from a key.</p>
    } @else {
      <ul>
        @for (app of apps(); track app.appId) {
          <li class="app">
            <div class="info">
              <span class="name">{{ app.displayName }}</span>
              <span class="path" [attr.title]="app.path">{{ app.path }}</span>
            </div>
            @if (confirming() === app.appId) {
              <button
                class="ld-btn ld-btn--danger"
                type="button"
                [disabled]="busy()"
                [attr.aria-label]="'Confirm removing ' + app.displayName"
                (click)="confirmRemove(app.appId)"
              >
                Remove?
              </button>
              <button class="ld-btn" type="button" (click)="confirming.set(null)">Keep</button>
            } @else {
              <button
                class="ld-btn"
                type="button"
                [disabled]="busy()"
                [attr.aria-label]="'Remove ' + app.displayName"
                (click)="confirming.set(app.appId)"
              >
                Remove
              </button>
            }
          </li>
        }
      </ul>
    }

    <form class="form" novalidate (submit)="submit($event)">
      <label class="ld-label" for="app-path">Program path</label>
      <input
        id="app-path"
        class="ld-field"
        type="text"
        autocomplete="off"
        spellcheck="false"
        placeholder="C:\\Program Files\\App\\app.exe"
        [attr.maxlength]="pathMaxLength + 2"
        [value]="path()"
        (input)="onPath($event)"
      />
      <label class="ld-label" for="app-name">Name</label>
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
      <button class="ld-btn" type="submit" [disabled]="busy()">Register app</button>
      <p class="hint">Tip: Shift + right-click the .exe and choose “Copy as path”, then paste it here.</p>
    </form>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-2);
    }

    h2 {
      font-size: var(--ld-font-size-base);
      font-weight: var(--ld-font-weight-bold);
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

    .app {
      display: flex;
      align-items: center;
      gap: var(--ld-space-3);
      padding: var(--ld-space-3) 0;
      border-top: 1px solid var(--ld-color-line);

      &:first-child {
        border-top: 0;
      }
    }

    .info {
      flex: 1;
      display: flex;
      flex-direction: column;
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
    }

    .path {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-xs);
    }

    .form {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-2);
      margin-top: var(--ld-space-2);
    }

    .ld-label {
      margin: var(--ld-space-1) 0 0;
      color: var(--ld-color-muted-on-panel);
    }

    .ld-field {
      min-height: 42px;
      font-size: var(--ld-font-size-md);
    }

    .form-error {
      min-height: 1.2em;
      font-size: var(--ld-font-size-sm);
    }

    .ld-btn {
      min-height: 40px;
      padding: 0 var(--ld-space-3);
      font-size: var(--ld-font-size-sm);
    }
  `,
  host: { role: 'region', 'aria-labelledby': 'apps-title' },
})
export class AppList {
  readonly apps = input.required<readonly RegisteredAppDto[]>();
  readonly busy = input(false);

  readonly register = output<AppRegistration>();
  readonly remove = output<string>();

  protected readonly nameMaxLength = APP_NAME_MAX_LENGTH;
  protected readonly pathMaxLength = APP_PATH_MAX_LENGTH;
  protected readonly confirming = signal<string | null>(null);
  protected readonly path = signal('');
  protected readonly name = signal('');
  protected readonly formError = signal<string | null>(null);
  /** Whether the name still follows the path (until the owner types one). */
  private nameFollowsPath = true;

  /** Clears the form, after the page saw the app registered. */
  reset(): void {
    this.path.set('');
    this.name.set('');
    this.formError.set(null);
    this.nameFollowsPath = true;
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

  protected confirmRemove(appId: string): void {
    this.confirming.set(null);
    this.remove.emit(appId);
  }
}
