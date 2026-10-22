import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { DeckFacade } from '../../../application/deck.facade';
import { DeckGrid } from '../../components/deck-grid/deck-grid';
import { PageDots } from '../../components/page-dots/page-dots';
import { ProfilePicker } from '../../components/profile-picker/profile-picker';

/**
 * `/deck`: the paired phone's control surface, inside the shell (which shows the connection badge). Connects as the
 * device on open; a rejected or revoked token sends the device back to pairing, once, with the reason.
 */
@Component({
  selector: 'app-deck-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DeckGrid, PageDots, ProfilePicker],
  templateUrl: './deck-page.html',
  styleUrl: './deck-page.scss',
})
export class DeckPage {
  protected readonly deck = inject(DeckFacade);
  private readonly router = inject(Router);

  protected readonly outdated = computed(() => this.deck.rejection() === 'protocolMismatch');

  constructor() {
    if (!this.deck.start()) {
      void this.router.navigateByUrl('/pair', { replaceUrl: true });
    }
    effect(() => {
      const rejection = this.deck.rejection();
      if (rejection === 'unauthorized' || rejection === 'deviceRevoked') {
        void this.router.navigate(['/pair'], { queryParams: { reason: rejection }, replaceUrl: true });
      }
    });
  }
}
