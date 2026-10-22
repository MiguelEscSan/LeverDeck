import type { ErrorCodeDto } from '../../protocol';

/**
 * Why the agent refused a connection for good. Retrying cannot help, so the connection stops instead of looping:
 *
 * - `unauthorized`: unknown device token (or wrong admin token) — pair again (or reopen the editor link).
 * - `deviceRevoked`: the device was revoked on the PC — pair again.
 * - `protocolMismatch`: this app and the agent speak different protocol versions — reload the app.
 */
export type ConnectionRejection = 'unauthorized' | 'deviceRevoked' | 'protocolMismatch';

/** WebSocket close code 1008, which the agent uses for every rejection. */
export const POLICY_VIOLATION = 1008;

const REJECTIONS: readonly string[] = ['unauthorized', 'deviceRevoked', 'protocolMismatch'];

const isRejection = (value: string): value is ConnectionRejection => REJECTIONS.includes(value);

/**
 * Reads a close frame: the agent closes with 1008 and the wire error code as the reason. Anything else (a drop, a
 * restart, `shutdown`, an oversized message) is worth reconnecting and returns `null`.
 */
export function rejectionFromClose(code: number, reason: string): ConnectionRejection | null {
  return code === POLICY_VIOLATION && isRejection(reason) ? reason : null;
}

/** The same decision for the `error` the agent sends just before closing. */
export function rejectionFromError(code: ErrorCodeDto): ConnectionRejection | null {
  return isRejection(code) ? code : null;
}
