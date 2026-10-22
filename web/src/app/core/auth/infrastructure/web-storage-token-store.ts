import type { TokenStore } from '../application/token-stores';

/**
 * A {@link TokenStore} on one key of a Web Storage area. Storage can be missing or throw (private mode, blocked
 * cookies, quota); then reads return `null` and writes are dropped.
 */
export class WebStorageTokenStore implements TokenStore {
  constructor(
    private readonly storage: () => Storage | null,
    private readonly key: string,
  ) {}

  read(): string | null {
    try {
      const value = this.storage()?.getItem(this.key) ?? null;
      return value ? value : null;
    } catch {
      return null;
    }
  }

  write(token: string): void {
    try {
      this.storage()?.setItem(this.key, token);
    } catch {
      // Not persisted: the token still works for this visit through the session state.
    }
  }

  clear(): void {
    try {
      this.storage()?.removeItem(this.key);
    } catch {
      // Nothing stored that could be removed.
    }
  }
}
