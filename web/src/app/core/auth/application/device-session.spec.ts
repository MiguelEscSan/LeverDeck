import { TestBed } from '@angular/core/testing';
import { DeviceSession } from './device-session';
import { DeviceTokenStore } from './token-stores';

class MemoryStore extends DeviceTokenStore {
  constructor(public value: string | null) {
    super();
  }

  read(): string | null {
    return this.value;
  }

  write(token: string): void {
    this.value = token;
  }

  clear(): void {
    this.value = null;
  }
}

describe('DeviceSession', () => {
  const create = (token: string | null) => {
    const store = new MemoryStore(token);
    TestBed.configureTestingModule({ providers: [{ provide: DeviceTokenStore, useValue: store }] });
    return { store, session: TestBed.inject(DeviceSession) };
  };

  it('starts from the stored token', () => {
    const { session } = create('t1');

    expect(session.paired()).toBe(true);
    expect(session.token()).toBe('t1');
  });

  it('remembers a new token and forgets it', () => {
    const { store, session } = create(null);
    expect(session.paired()).toBe(false);

    session.remember('t2');
    expect(store.value).toBe('t2');
    expect(session.paired()).toBe(true);

    session.forget();
    expect(store.value).toBeNull();
    expect(session.token()).toBeNull();
  });
});
