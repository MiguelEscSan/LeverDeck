import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { adminTokenGuard, entryGuard, pairedGuard } from './auth-guards';
import { AdminTokenStore, DeviceTokenStore, TokenStore } from './token-stores';

class MemoryStore extends TokenStore {
  constructor(public value: string | null = null) {
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

@Component({ changeDetection: ChangeDetectionStrategy.OnPush, template: '' })
class Blank {}

describe('auth guards', () => {
  let admin: MemoryStore;

  const navigate = async (url: string) => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return TestBed.inject(Router).url;
  };

  const setUp = (deviceToken: string | null) => {
    admin = new MemoryStore();
    TestBed.configureTestingModule({
      providers: [
        { provide: DeviceTokenStore, useValue: new MemoryStore(deviceToken) },
        { provide: AdminTokenStore, useValue: admin },
        provideRouter([
          { path: '', pathMatch: 'full', canActivate: [entryGuard], children: [] },
          { path: 'pair', component: Blank },
          { path: 'deck', component: Blank, canActivate: [pairedGuard] },
          { path: 'admin', component: Blank, canActivate: [adminTokenGuard] },
        ]),
      ],
    });
  };

  it('sends an unpaired device from / to pairing', async () => {
    setUp(null);

    expect(await navigate('/')).toBe('/pair');
  });

  it('sends a paired device from / straight to its deck', async () => {
    setUp('t1');

    expect(await navigate('/')).toBe('/deck');
  });

  it('keeps an unpaired device out of the deck', async () => {
    setUp(null);

    expect(await navigate('/deck')).toBe('/pair');
  });

  it('moves the editor link token from / into the store and off the URL', async () => {
    setUp('t1');

    expect(await navigate('/?admin=secret')).toBe('/admin');
    expect(admin.value).toBe('secret');
  });

  it('strips the admin token from /admin too', async () => {
    setUp(null);

    expect(await navigate('/admin?admin=secret')).toBe('/admin');
    expect(admin.value).toBe('secret');
  });

  it('opens /admin without a token in the URL', async () => {
    setUp(null);

    expect(await navigate('/admin')).toBe('/admin');
    expect(admin.value).toBeNull();
  });
});
