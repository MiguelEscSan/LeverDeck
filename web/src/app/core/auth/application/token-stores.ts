/**
 * Where a credential survives between visits. Reads never throw: blocked or unavailable browser storage behaves like
 * an empty store, so a private window still works for the current visit.
 */
export abstract class TokenStore {
  abstract read(): string | null;
  abstract write(token: string): void;
  abstract clear(): void;
}

/** The device token from pairing; it lives as long as the pairing (`localStorage` in the browser). */
export abstract class DeviceTokenStore extends TokenStore {}

/**
 * The admin token of the desktop editor, read once from `?admin=` and kept for this tab only (`sessionStorage`), so
 * it never stays in the address bar or the history.
 */
export abstract class AdminTokenStore extends TokenStore {}
