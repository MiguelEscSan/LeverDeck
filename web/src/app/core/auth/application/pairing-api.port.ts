import type { PairDeviceRequest } from '../../protocol';
import type { RedeemOutcome } from '../domain/redeem-outcome';

/** `POST /api/pairing/redeem`, the only call a device makes before it has a token. Never throws. */
export abstract class PairingApi {
  abstract redeem(request: PairDeviceRequest): Promise<RedeemOutcome>;
}
