import type { ErrorCodeDto, PairDeviceResponse } from '../../protocol';

/** Why redeeming a pairing code failed: a wire error code, or `network` when the agent could not be reached. */
export type RedeemFailure = ErrorCodeDto | 'network';

export type RedeemOutcome =
  | { readonly ok: true; readonly response: PairDeviceResponse }
  | { readonly ok: false; readonly reason: RedeemFailure };
