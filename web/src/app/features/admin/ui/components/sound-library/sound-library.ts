import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import type { SoundDto } from '../../../../../core/protocol';
import { formatDuration, formatSize, soundNameFromFile, soundUploadError } from '../../../../../core/sounds/domain/sound-clip';

/**
 * "Audio": the sound clips the owner uploaded from this PC. A WAV is chosen here (never from a phone), named, and
 * listed with its length and size; a clip can be played here, renamed and deleted. Deleting is two-step, and a clip a
 * key still plays is refused by the agent. Presentational.
 */
@Component({
  selector: 'app-sound-library',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="upload" (submit)="onSubmit($event)">
      <label class="ld-btn ld-btn--primary choose" [class.disabled]="uploading()">
        <input
          class="visually-hidden"
          type="file"
          accept=".wav,audio/wav"
          [disabled]="uploading()"
          (change)="onFile($event)"
        />
        Choose a WAV file
      </label>
      @if (pending(); as file) {
        <div class="pending">
          <label class="ld-label" for="sound-name">Name</label>
          <input id="sound-name" class="ld-field" type="text" [value]="name()" (input)="onName($event)" maxlength="60" />
          @if (fileError() ?? nameError(); as problem) {
            <p class="ld-error" role="alert">{{ problem }}</p>
          }
          <p class="hint">{{ file.name }} · {{ size(file.size) }}</p>
          <button type="submit" class="ld-btn ld-btn--primary" [disabled]="uploading() || !canUpload()">
            {{ uploading() ? 'Uploading…' : 'Upload' }}
          </button>
        </div>
      } @else {
        <p class="hint">Only WAV files play. Convert MP3, OGG or FLAC to WAV first. Up to 50 MB each.</p>
      }
    </form>

    @if (sounds().length === 0) {
      <p class="empty">No sounds yet. Upload a WAV to play it from any key.</p>
    } @else {
      <ul class="list" aria-label="Sounds">
        @for (sound of sounds(); track sound.id) {
          <li class="row">
            <button type="button" class="play" [attr.aria-label]="'Play ' + sound.name" [disabled]="busy()" (click)="preview.emit(sound.id)">
              ▶
            </button>
            <input
              class="ld-field name"
              type="text"
              maxlength="60"
              [attr.aria-label]="'Name of ' + sound.name"
              [value]="sound.name"
              [disabled]="busy()"
              (change)="onRename(sound, $event)"
            />
            <span class="meta">{{ length(sound.durationMs) }}</span>
            <span class="meta">{{ size(sound.sizeBytes) }}</span>
            @if (confirming() === sound.id) {
              <button type="button" class="ld-btn ld-btn--danger" [disabled]="busy()" (click)="confirmRemove(sound.id)">
                Delete {{ sound.name }}
              </button>
            } @else {
              <button type="button" class="link" [attr.aria-label]="'Delete ' + sound.name" [disabled]="busy()" (click)="confirming.set(sound.id)">
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

    .upload,
    .pending {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--ld-space-2);
    }

    .choose {
      position: relative;
      cursor: pointer;
    }

    .choose.disabled {
      opacity: 0.6;
    }

    .list {
      display: flex;
      flex-direction: column;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .row {
      display: grid;
      grid-template-columns: 40px minmax(0, 1fr) 80px 90px auto;
      align-items: center;
      gap: var(--ld-space-3);
      padding: var(--ld-space-3) var(--ld-space-2);
      border-bottom: 1px solid var(--ld-color-border);
    }

    .play {
      width: 40px;
      min-height: 40px;
      border: 0;
      border-radius: 50%;
      background: var(--ld-color-accent);
      color: var(--ld-color-on-accent, #fff);
      cursor: pointer;
    }

    .name {
      min-width: 0;
    }

    .meta,
    .hint,
    .empty {
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-sm);
    }
  `,
})
export class SoundLibrary {
  readonly sounds = input<readonly SoundDto[]>([]);
  readonly busy = input(false);
  readonly uploading = input(false);

  readonly upload = output<{ readonly file: File; readonly name: string }>();
  readonly rename = output<{ readonly soundId: string; readonly name: string }>();
  readonly remove = output<string>();
  readonly preview = output<string>();

  protected readonly pending = signal<File | null>(null);
  protected readonly name = signal('');
  protected readonly fileError = signal<string | null>(null);
  protected readonly confirming = signal<string | null>(null);

  protected readonly canUpload = () => {
    const file = this.pending();
    return file !== null && this.fileError() === null && this.nameError() === null;
  };

  protected length(durationMs: number): string {
    return formatDuration(durationMs);
  }

  protected size(bytes: number): string {
    return formatSize(bytes);
  }

  protected nameError(): string | null {
    const name = this.name().trim();
    return name.length >= 1 && name.length <= 60 ? null : 'Use 1–60 characters.';
  }

  protected onFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.pending.set(file);
    this.name.set(file ? soundNameFromFile(file.name) : '');
    this.fileError.set(file ? soundUploadError(file) : null);
  }

  protected onName(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    const file = this.pending();
    if (file && this.canUpload()) {
      this.upload.emit({ file, name: this.name() });
    }
  }

  protected onRename(sound: SoundDto, event: Event): void {
    const name = (event.target as HTMLInputElement).value.trim();
    if (name !== sound.name && name.length >= 1) {
      this.rename.emit({ soundId: sound.id, name });
    }
  }

  protected confirmRemove(soundId: string): void {
    this.confirming.set(null);
    this.remove.emit(soundId);
  }
}
