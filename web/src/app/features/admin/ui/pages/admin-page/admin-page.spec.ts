import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ConnectionStatus } from '../../../../../core/connection/domain/connection-status';
import type { DeviceDto, PairingSessionDto } from '../../../../../core/protocol';
import { type AdminAccess, AdminFacade } from '../../../application/admin.facade';
import { AdminPage } from './admin-page';

const device: DeviceDto = {
  id: 'd1',
  name: 'Pixel 8',
  pairedAt: '2026-10-20T10:00:00.000Z',
  lastSeenAt: '2026-10-22T12:00:00.000Z',
  connected: true,
  revoked: false,
};

class FakeAdmin {
  readonly access = signal<AdminAccess>('ready');
  readonly status = signal<ConnectionStatus>('connected');
  readonly session = signal<PairingSessionDto | null>(null);
  readonly devices = signal<DeviceDto[]>([device]);
  readonly now = signal(Date.parse('2026-10-22T12:00:00.000Z'));
  readonly secondsLeft = signal(0);
  readonly expired = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly calls: string[] = [];

  start(): void {
    this.calls.push('start');
  }

  async startPairing(): Promise<void> {
    this.calls.push('startPairing');
  }

  async cancelPairing(): Promise<void> {
    this.calls.push('cancelPairing');
  }

  async revoke(deviceId: string): Promise<void> {
    this.calls.push(`revoke ${deviceId}`);
  }
}

describe('AdminPage', () => {
  let admin: FakeAdmin;

  beforeEach(() => {
    admin = new FakeAdmin();
    TestBed.configureTestingModule({ providers: [{ provide: AdminFacade, useValue: admin }] });
  });

  async function render() {
    const fixture = TestBed.createComponent(AdminPage);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('connects on open and shows pairing and the devices once ready', async () => {
    const { element } = await render();

    expect(admin.calls).toEqual(['start']);
    expect(element.querySelector('app-pairing-card')).not.toBeNull();
    expect(element.querySelector('app-device-list')?.textContent).toContain('Pixel 8');
    expect(element.querySelector('.status')?.textContent?.trim()).toBe('Agent connected');
  });

  it('wires the pairing and revoke buttons to the facade', async () => {
    const { fixture, element } = await render();
    const button = (label: string) =>
      [...element.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === label);

    button('Start pairing')?.click();
    button('Revoke')?.click();
    await fixture.whenStable();
    button('Revoke?')?.click();

    expect(admin.calls).toEqual(['start', 'startPairing', 'revoke d1']);
  });

  it('asks for the editor link when the tab has no admin token', async () => {
    admin.access.set('missingToken');
    const { element } = await render();

    expect(element.textContent).toContain('Open the editor from its link');
    expect(element.querySelector('app-pairing-card')).toBeNull();
    expect(element.querySelector('.status')).toBeNull();
  });

  it('explains a rejected editor link', async () => {
    admin.access.set('rejected');
    const { element } = await render();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('This editor link is not valid');
  });

  it('shows the facade error above the cards', async () => {
    admin.error.set("Couldn't start pairing. Try again.");
    const { element } = await render();

    expect(element.querySelector('.error')?.textContent).toBe("Couldn't start pairing. Try again.");
  });
});
