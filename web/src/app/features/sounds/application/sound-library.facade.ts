import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { EventMessage, SoundDto } from '../../../core/protocol';

/**
 * The phone's view of the sound library: the clips the owner uploaded on the PC, read when the phone connects and kept
 * live by `soundsChanged`. Read-only: a phone never uploads, renames or deletes a clip.
 */
@Injectable({ providedIn: 'root' })
export class SoundLibraryFacade {
  private readonly connection = inject(ConnectionFacade);

  private readonly soundsState = signal<SoundDto[]>([]);
  private readonly errorState = signal<string | null>(null);

  readonly sounds = this.soundsState.asReadonly();
  readonly error = this.errorState.asReadonly();

  constructor() {
    effect(() => {
      if (this.connection.connected()) {
        untracked(() => void this.load());
      }
    });
    this.connection.events.pipe(takeUntilDestroyed()).subscribe((event) => this.onEvent(event));
  }

  private async load(): Promise<void> {
    try {
      this.soundsState.set((await this.connection.query('getSounds', {})).sounds);
      this.errorState.set(null);
    } catch {
      this.errorState.set("Couldn't read the sound library.");
    }
  }

  private onEvent(event: EventMessage): void {
    if (event.type === 'soundsChanged') {
      this.soundsState.set(event.payload.sounds);
    }
  }
}
