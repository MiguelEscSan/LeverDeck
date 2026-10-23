import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SoundLibraryFacade } from '../../../application/sound-library.facade';
import { SoundList } from '../../components/sound-list/sound-list';

/** `/sounds` on the phone: the PC's sound clips, read-only. Uploads happen only in the editor on the PC. */
@Component({
  selector: 'app-sound-library-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, SoundList],
  template: `
    <nav class="back"><a routerLink="/deck">← Deck</a></nav>
    <h1 class="title">Audio</h1>
    <p class="sub">Sounds on this PC. Add or rename them in the editor on the PC.</p>
    @if (library.error(); as error) {
      <p class="ld-error" role="alert">{{ error }}</p>
    }
    <app-sound-list [sounds]="library.sounds()" />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-3);
    }

    .back a {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
    }

    .title {
      margin: 0;
      font-size: var(--ld-font-size-xl, 24px);
    }

    .sub {
      margin: 0;
      color: var(--ld-color-muted);
    }
  `,
})
export class SoundLibraryPage {
  protected readonly library = inject(SoundLibraryFacade);
}
