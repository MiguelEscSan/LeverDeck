import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { SoundDto } from '../../../../../core/protocol';
import { formatDuration, formatSize } from '../../../../../core/sounds/domain/sound-clip';

/** The clips on the PC, by name, with their length and size. Read-only; presentational. */
@Component({
  selector: 'app-sound-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (sounds().length === 0) {
      <p class="empty">No sounds yet. Add a WAV on the PC in the editor's Audio section.</p>
    } @else {
      <ul class="list" aria-label="Sounds on this PC">
        @for (sound of sounds(); track sound.id) {
          <li class="row">
            <span class="name">{{ sound.name }}</span>
            <span class="meta">{{ length(sound.durationMs) }} · {{ size(sound.sizeBytes) }}</span>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .list {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .row {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-height: 44px;
      padding: var(--ld-space-3);
      border-radius: var(--ld-radius-md, 12px);
      background: var(--ld-color-surface);
    }

    .name {
      overflow: hidden;
      font-weight: var(--ld-font-weight-semibold);
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .meta,
    .empty {
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-sm);
    }
  `,
})
export class SoundList {
  readonly sounds = input<readonly SoundDto[]>([]);

  protected length(durationMs: number): string {
    return formatDuration(durationMs);
  }

  protected size(bytes: number): string {
    return formatSize(bytes);
  }
}
