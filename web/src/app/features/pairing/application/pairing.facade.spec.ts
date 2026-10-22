import { TestBed } from '@angular/core/testing';
import { PairingApi } from '../../../core/auth/application/pairing-api.port';
import { DeviceSession } from '../../../core/auth/application/device-session';
import type { RedeemOutcome } from '../../../core/auth/domain/redeem-outcome';
import type { PairDeviceRequest } from '../../../core/protocol';
import { PairingFacade } from './pairing.facade';

class FakePairingApi extends PairingApi {
  readonly requests: PairDeviceRequest[] = [];
  outcome: RedeemOutcome = { ok: true, response: { deviceId: 'd1', deviceToken: 'token-1' } };

  async redeem(request: PairDeviceRequest): Promise<RedeemOutcome> {
    this.requests.push(request);
    return this.outcome;
  }
}

const PIXEL = 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/130.0 Mobile Safari/537.36';

describe('PairingFacade', () => {
  let api: FakePairingApi;
  let remembered: string[];
  let facade: PairingFacade;

  beforeEach(() => {
    api = new FakePairingApi();
    remembered = [];
    TestBed.configureTestingModule({
      providers: [
        PairingFacade,
        { provide: PairingApi, useValue: api },
        { provide: DeviceSession, useValue: { remember: (token: string) => remembered.push(token) } },
      ],
    });
    facade = TestBed.inject(PairingFacade);
  });

  it('pre-fills the code from the QR link and the name from the user agent', () => {
    facade.setUp({ code: '472915', userAgent: PIXEL });

    expect(facade.code()).toBe('472915');
    expect(facade.deviceName()).toBe('Pixel 8');
    expect(facade.canPair()).toBe(true);
  });

  it('cannot pair until the code is complete and the name is not blank', () => {
    facade.setUp({ code: null, userAgent: PIXEL });
    expect(facade.canPair()).toBe(false);

    facade.setCode('4729');
    expect(facade.canPair()).toBe(false);

    facade.setCode('472915');
    facade.setDeviceName('   ');
    expect(facade.canPair()).toBe(false);
  });

  it('redeems the code, keeps the token and reports success', async () => {
    facade.setUp({ code: '472915', userAgent: PIXEL });
    facade.setDeviceName('  Kitchen tablet ');

    const paired = await facade.pair();

    expect(paired).toBe(true);
    expect(api.requests).toEqual([{ code: '472915', deviceName: 'Kitchen tablet' }]);
    expect(remembered).toEqual(['token-1']);
    expect(facade.error()).toBeNull();
  });

  it('shows a clear message for a wrong code and keeps the form', async () => {
    api.outcome = { ok: false, reason: 'pairingCodeInvalid' };
    facade.setUp({ code: '000000', userAgent: PIXEL });

    expect(await facade.pair()).toBe(false);

    expect(facade.error()).toMatch(/not right/);
    expect(facade.code()).toBe('000000');
    expect(remembered).toEqual([]);
    expect(facade.busy()).toBe(false);
  });

  it('shows a clear message for an expired code', async () => {
    api.outcome = { ok: false, reason: 'pairingCodeExpired' };
    facade.setUp({ code: '472915', userAgent: PIXEL });

    await facade.pair();

    expect(facade.error()).toMatch(/expired/);
  });

  it('clears the error once the code is edited', async () => {
    api.outcome = { ok: false, reason: 'pairingCodeInvalid' };
    facade.setUp({ code: '000000', userAgent: PIXEL });
    await facade.pair();

    facade.setCode('11');

    expect(facade.error()).toBeNull();
  });

  it('is busy while redeeming and ignores a second submit', async () => {
    facade.setUp({ code: '472915', userAgent: PIXEL });

    const first = facade.pair();
    expect(facade.busy()).toBe(true);
    const second = await facade.pair();

    expect(second).toBe(false);
    await first;
    expect(api.requests).toHaveLength(1);
  });
});
