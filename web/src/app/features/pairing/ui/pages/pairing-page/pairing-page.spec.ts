import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { DeviceSession } from '../../../../../core/auth/application/device-session';
import { PairingApi } from '../../../../../core/auth/application/pairing-api.port';
import type { RedeemOutcome } from '../../../../../core/auth/domain/redeem-outcome';
import { PairingPage } from './pairing-page';

@Component({ changeDetection: ChangeDetectionStrategy.OnPush, template: 'deck' })
class DeckStub {}

describe('PairingPage', () => {
  let outcome: RedeemOutcome;
  let remembered: string[];

  beforeEach(() => {
    outcome = { ok: true, response: { deviceId: 'd1', deviceToken: 'token-1' } };
    remembered = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'pair', component: PairingPage },
          { path: 'deck', component: DeckStub },
        ]),
        { provide: PairingApi, useValue: { redeem: async () => outcome } },
        { provide: DeviceSession, useValue: { remember: (token: string) => remembered.push(token) } },
      ],
    });
  });

  const open = async (url: string) => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    await harness.fixture.whenStable();
    return { harness, element: harness.routeNativeElement as HTMLElement };
  };

  const submit = async (harness: RouterTestingHarness, element: HTMLElement) => {
    element.querySelector('form')?.dispatchEvent(new Event('submit'));
    await harness.fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    await harness.fixture.whenStable();
  };

  it('pre-fills the code from the QR link', async () => {
    const { element } = await open('/pair?code=472915');

    const digits = [...element.querySelectorAll('.digit')].map((digit) => digit.textContent?.trim());
    expect(digits).toEqual(['4', '7', '2', '9', '1', '5']);
    expect(element.querySelector<HTMLButtonElement>('button[type=submit]')?.disabled).toBe(false);
  });

  it('cannot submit before the code is complete', async () => {
    const { element } = await open('/pair');

    expect(element.querySelector<HTMLButtonElement>('button[type=submit]')?.disabled).toBe(true);
  });

  it('pairs and goes to the deck', async () => {
    const { harness, element } = await open('/pair?code=472915');

    await submit(harness, element);

    expect(remembered).toEqual(['token-1']);
    expect(TestBed.inject(Router).url).toBe('/deck');
  });

  it('shows the refusal and stays on the page', async () => {
    outcome = { ok: false, reason: 'pairingCodeExpired' };
    const { harness, element } = await open('/pair?code=472915');

    await submit(harness, element);

    expect(element.querySelector('[role=alert]')?.textContent).toMatch(/expired/);
    expect(TestBed.inject(Router).url).toBe('/pair?code=472915');
  });

  it('explains why a revoked device is back here', async () => {
    const { element } = await open('/pair?reason=deviceRevoked');

    expect(element.querySelector('.notice')?.textContent).toMatch(/removed on your PC/);
  });
});
