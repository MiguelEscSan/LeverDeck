import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { KEY_ICONS } from '../../../../../core/keys/domain/key-icons';
import type {
  ButtonDto,
  InstalledGameDto,
  KeyColorDto,
  ProfileSummaryDto,
  RegisteredAppDto,
} from '../../../../../core/protocol';
import { type ButtonChange, LABEL_MAX_LENGTH } from '../../../domain/profile-editing';
import { type ActionChoice, ActionEditor } from '../action-editor/action-editor';
import { ColorSwatches } from '../color-swatches/color-swatches';
import type { GridPosition } from '../editor-grid/editor-grid';
import { KeyFace } from '../key-face/key-face';

/**
 * "Selected key" (docs/mocks/desktop.html): the label (saved when the field is left or Enter is pressed), color,
 * icon and action of the chosen key, or a button to add one on a chosen empty position. Presentational.
 */
@Component({
  selector: 'app-key-inspector',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ActionEditor, ColorSwatches, KeyFace],
  template: `
    <h2 id="inspector-title">Selected key</h2>
    @if (button(); as button) {
      <div class="preview"><app-key-face [button]="button" /></div>

      <label class="ld-label" for="key-label">Label</label>
      <input
        id="key-label"
        class="ld-field"
        type="text"
        autocomplete="off"
        placeholder="No label"
        [attr.maxlength]="labelMaxLength"
        [value]="button.label"
        (change)="onLabel($event)"
        (keydown.enter)="blur($event)"
      />

      <span class="ld-label" id="key-color-label">Color</span>
      <app-color-swatches label="Key color" [value]="button.color" (chosen)="onColor($event)" />

      <span class="ld-label" id="key-icon-label">Icon</span>
      <div class="icons" role="radiogroup" aria-labelledby="key-icon-label">
        @for (icon of customIcons(); track icon) {
          <button
            type="button"
            role="radio"
            class="icon"
            [attr.aria-checked]="icon === button.iconId"
            aria-label="Your icon"
            (click)="changed.emit({ iconId: icon })"
          >
            <app-key-face [button]="{ label: '', color: button.color, iconId: icon }" />
          </button>
        }
        @for (icon of icons; track icon.id) {
          <button
            type="button"
            role="radio"
            class="icon"
            [attr.aria-checked]="icon.id === button.iconId"
            [attr.aria-label]="icon.name"
            [attr.title]="icon.name"
            (click)="changed.emit({ iconId: icon.id })"
          >
            <app-key-face [button]="{ label: '', color: button.color, iconId: icon.id }" />
          </button>
        }
      </div>

      <label class="link upload" [class.disabled]="uploading()">
        <input
          class="visually-hidden"
          type="file"
          accept="image/*"
          [disabled]="uploading()"
          (change)="onFile($event)"
        />
        {{ uploading() ? 'Uploading…' : 'Upload your own icon…' }}
      </label>

      <app-action-editor
        [action]="button.action"
        [profileId]="profileId()"
        [profiles]="profiles()"
        [pageCount]="pageCount()"
        [apps]="apps()"
        [games]="games()"
        [gamesLoading]="gamesLoading()"
        (changed)="actionChanged.emit($event)"
        (refreshGames)="refreshGames.emit()"
      />

      <button class="ld-btn ld-btn--danger remove" type="button" (click)="remove.emit()">Remove key</button>
    } @else if (emptyAt(); as position) {
      <p class="hint">Row {{ position.row + 1 }}, column {{ position.column + 1 }} is empty.</p>
      <button class="ld-btn ld-btn--primary" type="button" (click)="add.emit()">Add a key here</button>
    } @else {
      <p class="hint">Choose a key on the grid to edit it, or an empty position to add one.</p>
    }
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

    .preview {
      width: 88px;
      margin: var(--ld-space-2) auto;
    }

    .ld-label {
      margin: var(--ld-space-2) 0 0;
      color: var(--ld-color-muted-on-panel);
    }

    .ld-field {
      min-height: 42px;
      font-size: var(--ld-font-size-md);
    }

    .icons {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(40px, 1fr));
      gap: var(--ld-space-2);
    }

    .icon {
      padding: 0;
      border: 0;
      border-radius: var(--ld-radius-sm);
      background: none;
      cursor: pointer;

      &[aria-checked='true'] {
        outline: 3px solid var(--ld-color-focus);
        outline-offset: 2px;
      }
    }

    .hint {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }

    .link {
      align-self: flex-start;
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
      text-decoration: underline;
      cursor: pointer;

      &.disabled {
        cursor: default;
        opacity: 0.55;
      }

      &:focus-within {
        outline: 3px solid var(--ld-color-focus);
        outline-offset: 2px;
      }
    }

    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }

    .remove {
      margin-top: var(--ld-space-3);
    }
  `,
  host: { role: 'region', 'aria-labelledby': 'inspector-title' },
})
export class KeyInspector {
  readonly button = input<ButtonDto | null>(null);
  readonly emptyAt = input<GridPosition | null>(null);
  readonly profileId = input.required<string>();
  readonly profiles = input.required<readonly ProfileSummaryDto[]>();
  readonly pageCount = input.required<number>();
  readonly apps = input<readonly RegisteredAppDto[]>([]);
  readonly games = input<readonly InstalledGameDto[]>([]);
  readonly gamesLoading = input(false);
  /** Uploaded icon ids, offered before the bundled icons. */
  readonly customIcons = input<readonly string[]>([]);
  readonly uploading = input(false);

  readonly changed = output<Omit<ButtonChange, 'action'>>();
  readonly actionChanged = output<ActionChoice>();
  readonly refreshGames = output();
  /** A picture to upload as an icon for this key. */
  readonly uploadIcon = output<File>();
  readonly add = output();
  readonly remove = output();

  protected readonly icons = KEY_ICONS;
  protected readonly labelMaxLength = LABEL_MAX_LENGTH;

  protected onLabel(event: Event): void {
    this.changed.emit({ label: (event.target as HTMLInputElement).value });
  }

  protected onColor(color: KeyColorDto): void {
    this.changed.emit({ color });
  }

  protected onFile(event: Event): void {
    const field = event.target as HTMLInputElement;
    const file = field.files?.[0];
    field.value = '';
    if (file) {
      this.uploadIcon.emit(file);
    }
  }

  protected blur(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }
}
