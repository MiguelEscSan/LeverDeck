import type { ErrorCodeDto, ErrorDto, PairDeviceRequest, PairDeviceResponse } from '../../protocol';
import { PairingApi } from '../application/pairing-api.port';
import type { RedeemOutcome } from '../domain/redeem-outcome';

export const REDEEM_PATH = '/api/pairing/redeem';

type Fetch = (input: string, init: RequestInit) => Promise<Response>;

const isErrorDto = (value: unknown): value is ErrorDto =>
  typeof value === 'object' &&
  value !== null &&
  typeof (value as ErrorDto).code === 'string' &&
  typeof (value as ErrorDto).message === 'string';

const isPairDeviceResponse = (value: unknown): value is PairDeviceResponse =>
  typeof value === 'object' &&
  value !== null &&
  typeof (value as PairDeviceResponse).deviceId === 'string' &&
  typeof (value as PairDeviceResponse).deviceToken === 'string' &&
  (value as PairDeviceResponse).deviceToken.length > 0;

/** {@link PairingApi} over `fetch` on the page origin: JSON in, `PairDeviceResponse` or `ErrorDto` out. */
export class FetchPairingApi extends PairingApi {
  constructor(private readonly fetch: Fetch) {
    super();
  }

  async redeem(request: PairDeviceRequest): Promise<RedeemOutcome> {
    let response: Response;
    try {
      response = await this.fetch(REDEEM_PATH, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: request.code, deviceName: request.deviceName }),
        cache: 'no-store',
        credentials: 'omit',
      });
    } catch {
      return { ok: false, reason: 'network' };
    }

    const body: unknown = await response.json().catch(() => null);
    if (response.ok && isPairDeviceResponse(body)) {
      return { ok: true, response: { deviceId: body.deviceId, deviceToken: body.deviceToken } };
    }
    const reason: ErrorCodeDto = isErrorDto(body) ? body.code : 'internal';
    return { ok: false, reason };
  }
}
