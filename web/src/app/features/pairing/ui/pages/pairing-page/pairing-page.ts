import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PairingFacade } from '../../../application/pairing.facade';
import { DEVICE_NAME_MAX_LENGTH, rejectionNotice } from '../../../domain/pairing-form';
import { CodeInput } from '../../components/code-input/code-input';
import { PairingHero } from '../../components/pairing-hero/pairing-hero';

/**
 * `/pair` and `/pair?code=…` (the QR link): the phone side of pairing (docs/mocks/login.html). The camera app opens
 * the QR link, so the code arrives pre-filled; otherwise the owner types the code shown on the PC.
 */
@Component({
  selector: 'app-pairing-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CodeInput, PairingHero],
  providers: [PairingFacade],
  templateUrl: './pairing-page.html',
  styleUrl: './pairing-page.scss',
})
export class PairingPage {
  protected readonly facade = inject(PairingFacade);
  private readonly router = inject(Router);
  private readonly window = inject(DOCUMENT).defaultView;

  protected readonly nameMaxLength = DEVICE_NAME_MAX_LENGTH;
  /** The PC this page came from: each PC serves the app under its own name. */
  protected readonly host = this.window?.location.hostname ?? '';
  protected readonly notice: string | null;

  constructor() {
    const query = inject(ActivatedRoute).snapshot.queryParamMap;
    this.notice = rejectionNotice(query.get('reason'));
    this.facade.setUp({ code: query.get('code'), userAgent: this.window?.navigator.userAgent ?? '' });
  }

  protected onNameInput(event: Event): void {
    this.facade.setDeviceName((event.target as HTMLInputElement).value);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (await this.facade.pair()) {
      await this.router.navigateByUrl('/deck', { replaceUrl: true });
    }
  }
}
