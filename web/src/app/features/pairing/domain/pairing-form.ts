import type { RedeemFailure } from '../../../core/auth/domain/redeem-outcome';

/** The agent's pairing code: exactly 6 ASCII digits. */
export const CODE_LENGTH = 6;

/** The agent accepts device names of 1–40 characters. */
export const DEVICE_NAME_MAX_LENGTH = 40;

/** Keeps the ASCII digits of whatever was typed, pasted or carried by the QR link, up to {@link CODE_LENGTH}. */
export const normalizeCode = (raw: string | null): string =>
  (raw ?? '').replace(/[^0-9]/g, '').slice(0, CODE_LENGTH);

export const isCompleteCode = (code: string): boolean => /^[0-9]{6}$/.test(code);

export const normalizeDeviceName = (name: string): string =>
  name.trim().slice(0, DEVICE_NAME_MAX_LENGTH).trim();

/**
 * A readable default for the device name, from the user agent: the Android model when the browser still reports it
 * (reduced user agents send `K`), otherwise the kind of device. The owner can edit it before pairing.
 */
export function defaultDeviceName(userAgent: string): string {
  const android = /Android [^;)]*;\s*([^;)]+?)(?:\sBuild\/[^;)]*)?\)/.exec(userAgent);
  if (android) {
    const model = android[1].trim();
    if (model.length > 1 && model !== 'K') {
      return normalizeDeviceName(model);
    }
    return /Mobile/.test(userAgent) ? 'Android phone' : 'Android tablet';
  }
  if (/\bAndroid\b/.test(userAgent)) {
    return 'Android phone';
  }
  if (/\biPhone\b/.test(userAgent)) {
    return 'iPhone';
  }
  if (/\biPad\b/.test(userAgent)) {
    return 'iPad';
  }
  if (/\bWindows\b/.test(userAgent)) {
    return 'Windows PC';
  }
  if (/\bMacintosh\b/.test(userAgent)) {
    return 'Mac';
  }
  return 'My device';
}

/** What to tell the owner when redeeming the code failed. */
export function redeemErrorMessage(reason: RedeemFailure): string {
  switch (reason) {
    case 'pairingCodeInvalid':
      return 'That code is not right. Check the code on your PC and try again.';
    case 'pairingCodeExpired':
      return 'That code has expired. Start pairing again on your PC to get a new one.';
    case 'validationFailed':
      return `Check the code and the device name (1–${DEVICE_NAME_MAX_LENGTH} characters).`;
    case 'network':
      return "Can't reach your PC. Check that LeverDeck is running and that this device is on the same network.";
    default:
      return 'Pairing failed on your PC. Try again.';
  }
}

/** Why the deck sent the device back to pairing (`?reason=` set by the deck), or `null` for a first pairing. */
export function rejectionNotice(reason: string | null): string | null {
  switch (reason) {
    case 'deviceRevoked':
      return 'This device was removed on your PC. Pair it again to keep using it.';
    case 'unauthorized':
      return 'This device is no longer paired with your PC. Pair it again to keep using it.';
    default:
      return null;
  }
}
