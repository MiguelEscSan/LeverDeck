import { WebStorageTokenStore } from './web-storage-token-store';

class MemoryStorage implements Storage {
  private readonly items = new Map<string, string>();

  get length(): number {
    return this.items.size;
  }

  clear(): void {
    this.items.clear();
  }

  getItem(key: string): string | null {
    return this.items.get(key) ?? null;
  }

  key(index: number): string | null {
    return [...this.items.keys()][index] ?? null;
  }

  removeItem(key: string): void {
    this.items.delete(key);
  }

  setItem(key: string, value: string): void {
    this.items.set(key, value);
  }
}

describe('WebStorageTokenStore', () => {
  it('writes, reads and clears one key', () => {
    const storage = new MemoryStorage();
    const store = new WebStorageTokenStore(() => storage, 'leverdeck.deviceToken');

    expect(store.read()).toBeNull();
    store.write('t1');
    expect(store.read()).toBe('t1');
    expect(storage.getItem('leverdeck.deviceToken')).toBe('t1');

    store.clear();
    expect(store.read()).toBeNull();
  });

  it('reads an empty value as no token', () => {
    const storage = new MemoryStorage();
    storage.setItem('k', '');

    expect(new WebStorageTokenStore(() => storage, 'k').read()).toBeNull();
  });

  it('behaves like an empty store when storage is blocked', () => {
    const blocked = (): Storage => {
      throw new DOMException('denied', 'SecurityError');
    };
    const store = new WebStorageTokenStore(blocked, 'k');

    expect(() => store.write('t1')).not.toThrow();
    expect(store.read()).toBeNull();
    expect(() => store.clear()).not.toThrow();
  });
});
