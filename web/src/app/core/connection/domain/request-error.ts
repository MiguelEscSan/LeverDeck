import type { ErrorCodeDto } from '../../protocol';

/**
 * Why a request failed: the agent's wire error code, or a local reason:
 *
 * - `timeout`: no answer in time.
 * - `notConnected`: the request could not be sent.
 * - `connectionLost`: the connection closed before the answer arrived.
 */
export type RequestFailure = ErrorCodeDto | 'timeout' | 'notConnected' | 'connectionLost';

export class RequestError extends Error {
  constructor(
    readonly code: RequestFailure,
    message: string = code,
  ) {
    super(message);
    this.name = 'RequestError';
  }
}

/** The failure code of anything a request promise rejected with. */
export const failureOf = (error: unknown): RequestFailure =>
  error instanceof RequestError ? error.code : 'internal';
