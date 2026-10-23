import { TestBed } from '@angular/core/testing';
import type { PairingSessionDto } from '../../../../../core/protocol';
import { PairingCard } from './pairing-card';

const session: PairingSessionDto = {
  code: '472915',
  qrUrl: 'http://leverdeck-office-pc.local:8765/pair?code=472915',
  fallbackUrl: 'http://192.168.1.20:8765/pair?code=472915',
  expiresAt: '2026-10-22T08:35:00.000Z',
};

async function render(inputs: { session: PairingSessionDto | null; secondsLeft: number; expired: boolean }) {
  const fixture = TestBed.createComponent(PairingCard);
  fixture.componentRef.setInput('session', inputs.session);
  fixture.componentRef.setInput('secondsLeft', inputs.secondsLeft);
  fixture.componentRef.setInput('expired', inputs.expired);
  await fixture.whenStable();
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

describe('PairingCard', () => {
  it('shows the QR, the code, the countdown and the fallback address of a live session', async () => {
    const { element } = await render({ session, secondsLeft: 272, expired: false });

    expect(element.querySelector('app-qr-code')).not.toBeNull();
    expect([...element.querySelectorAll('.code span')].map((digit) => digit.textContent)).toEqual([
      '4', '7', '2', '9', '1', '5',
    ]);
    expect(element.querySelector('.code')?.getAttribute('aria-label')).toBe('Pairing code 4 7 2 9 1 5');
    expect(element.querySelector('.expiry')?.textContent).toContain('4:32');
    expect(element.querySelector('.lead')?.textContent).toContain('leverdeck-office-pc.local:8765');
    expect(element.querySelector('.fallback')?.textContent).toContain('http://192.168.1.20:8765/pair?code=472915');
  });

  it('only offers typing the code at an address it could read from the link', async () => {
    const { element } = await render({ session: { ...session, qrUrl: 'not a link' }, secondsLeft: 272, expired: false });

    expect(element.querySelector('.lead')?.textContent?.replace(/\s+/g, ' ').trim()).toBe("Scan with your phone's camera.");
    expect(element.querySelector('.fallback')?.textContent).toContain('If the QR link doesn');
  });

  it('emits cancel from a live session', async () => {
    const { fixture, element } = await render({ session, secondsLeft: 272, expired: false });
    let cancelled = 0;
    fixture.componentInstance.cancelPairing.subscribe(() => cancelled++);

    element.querySelector('button')?.click();

    expect(element.querySelector('button')?.textContent?.trim()).toBe('Cancel');
    expect(cancelled).toBe(1);
  });

  it('offers to start pairing when there is no session', async () => {
    const { fixture, element } = await render({ session: null, secondsLeft: 0, expired: false });
    let started = 0;
    fixture.componentInstance.startPairing.subscribe(() => started++);

    expect(element.querySelector('app-qr-code')).toBeNull();
    const button = element.querySelector('button');
    expect(button?.textContent?.trim()).toBe('Start pairing');
    button?.click();

    expect(started).toBe(1);
  });

  it('hides an expired code and offers a new one', async () => {
    const { element } = await render({ session, secondsLeft: 0, expired: true });

    expect(element.querySelector('.code')).toBeNull();
    expect(element.textContent).toContain('The code expired.');
    expect(element.querySelector('button')?.textContent?.trim()).toBe('New code');
  });
});
