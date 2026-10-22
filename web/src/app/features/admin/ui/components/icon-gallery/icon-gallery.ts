import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { customIconUrl } from '../../../../../core/keys/domain/key-icons';

/**
 * "Icons": the pictures the owner uploaded for keys (a game's or an app's logo, a photo…). Pick a file or drop it on
 * the gallery; the editor turns it into a 256 × 256 PNG. Deleting is two-step. Presentational.
 */
@Component({
  selector: 'app-icon-gallery',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="drop"
      [class.over]="over()"
      (dragover)="onDragOver($event)"
      (dragleave)="over.set(false)"
      (drop)="onDrop($event)"
    >
      <p class="hint">
        Drop a picture here or choose one. PNG, JPG, WebP or GIF; it is resized to 256 × 256 and kept on your PC.
      </p>
      <label class="ld-btn ld-btn--primary upload" [class.disabled]="uploading()">
        <input
          class="visually-hidden"
          type="file"
          accept="image/*"
          [disabled]="uploading()"
          (change)="onFile($event)"
        />
        {{ uploading() ? 'Uploading…' : 'Upload an icon' }}
      </label>
    </div>

    @if (icons().length === 0) {
      <p class="hint empty">No icons yet.</p>
    } @else {
      <ul class="grid">
        @for (icon of icons(); track icon) {
          <li class="tile">
            <img [src]="url(icon)" alt="" draggable="false" />
            @if (confirming() === icon) {
              <button
                class="ld-btn ld-btn--danger"
                type="button"
                [disabled]="busy()"
                aria-label="Confirm deleting this icon"
                (click)="confirmRemove(icon)"
              >
                Delete?
              </button>
              <button class="ld-btn" type="button" (click)="confirming.set(null)">Keep</button>
            } @else {
              <button
                class="ld-btn"
                type="button"
                [disabled]="busy()"
                aria-label="Delete this icon"
                (click)="confirming.set(icon)"
              >
                Delete
              </button>
            }
          </li>
        }
      </ul>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-4);
    }

    .drop {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--ld-space-4);
      padding: var(--ld-space-5);
      border: 2px dashed var(--ld-color-field-border);
      border-radius: var(--ld-radius-lg);

      &.over {
        border-color: var(--ld-color-focus);
        background: color-mix(in srgb, var(--ld-color-panel) 60%, transparent);
      }
    }

    .hint {
      flex: 1;
      min-width: 220px;
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-sm);
    }

    .upload {
      min-height: 48px;
      font-size: var(--ld-font-size-md);

      &.disabled {
        opacity: 0.55;
        cursor: default;
      }

      &:focus-within {
        outline: 3px solid var(--ld-color-focus);
        outline-offset: 3px;
      }
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
      gap: var(--ld-space-4);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .tile {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--ld-space-2);
      padding: var(--ld-space-3);
      border-radius: var(--ld-radius-md);
      background: var(--ld-color-panel);

      img {
        width: 72px;
        height: 72px;
        padding: var(--ld-space-2);
        border-radius: var(--ld-radius-md);
        background: var(--ld-key-dark);
        object-fit: contain;
      }
    }

    .ld-btn {
      min-height: 36px;
      padding: 0 var(--ld-space-3);
      font-size: var(--ld-font-size-sm);
    }

    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
})
export class IconGallery {
  readonly icons = input.required<readonly string[]>();
  readonly busy = input(false);
  readonly uploading = input(false);

  readonly upload = output<File>();
  readonly remove = output<string>();

  protected readonly confirming = signal<string | null>(null);
  protected readonly over = signal(false);

  protected url(icon: string): string {
    return customIconUrl(icon);
  }

  protected onFile(event: Event): void {
    const field = event.target as HTMLInputElement;
    const file = field.files?.[0];
    field.value = '';
    if (file) {
      this.upload.emit(file);
    }
  }

  protected onDragOver(event: DragEvent): void {
    if (event.dataTransfer?.types.includes('Files')) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
      this.over.set(true);
    }
  }

  protected onDrop(event: DragEvent): void {
    this.over.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      event.preventDefault();
      this.upload.emit(file);
    }
  }

  protected confirmRemove(icon: string): void {
    this.confirming.set(null);
    this.remove.emit(icon);
  }
}
