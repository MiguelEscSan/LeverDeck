/** Whole seconds until `expiresAt` (ISO 8601), rounded up and never negative; unreadable dates count as expired. */
export function secondsLeft(expiresAt: string, nowMs: number): number {
  const expires = Date.parse(expiresAt);
  if (Number.isNaN(expires)) {
    return 0;
  }
  return Math.max(0, Math.ceil((expires - nowMs) / 1000));
}

/** `m:ss`, as in "Code expires in 4:32". */
export function formatCountdown(seconds: number): string {
  const whole = Math.max(0, Math.floor(seconds));
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`;
}

/** A short relative time for a device that is not connected. */
export function lastSeenLabel(lastSeenAt: string, nowMs: number): string {
  const minutes = Math.floor((nowMs - Date.parse(lastSeenAt)) / 60_000);
  if (Number.isNaN(minutes) || minutes < 1) {
    return 'Last seen just now';
  }
  if (minutes < 60) {
    return `Last seen ${minutes} min ago`;
  }
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `Last seen ${hours} h ago`;
  }
  return `Last seen ${Math.floor(hours / 24)} d ago`;
}
