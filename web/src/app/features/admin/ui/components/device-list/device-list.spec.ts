import { TestBed } from '@angular/core/testing';
import type { DeviceDto } from '../../../../../core/protocol';
import { DeviceList } from './device-list';

const pixel: DeviceDto = {
  id: 'd1',
  name: 'Pixel 8',
  pairedAt: '2026-10-20T10:00:00.000Z',
  lastSeenAt: '2026-10-22T12:00:00.000Z',
  connected: true,
  revoked: false,
};

const ipad: DeviceDto = {
  ...pixel,
  id: 'd2',
  name: 'iPad Air',
  lastSeenAt: '2026-10-22T10:00:00.000Z',
  connected: false,
};

async function render(devices: DeviceDto[]) {
  const fixture = TestBed.createComponent(DeviceList);
  fixture.componentRef.setInput('devices', devices);
  fixture.componentRef.setInput('now', Date.parse('2026-10-22T12:00:00.000Z'));
  await fixture.whenStable();
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

describe('DeviceList', () => {
  it('shows each device with its state', async () => {
    const { element } = await render([pixel, ipad]);
    const rows = [...element.querySelectorAll('li')];

    expect(rows.map((row) => row.querySelector('.name')?.textContent)).toEqual(['Pixel 8', 'iPad Air']);
    expect(rows[0].querySelector('.state')?.textContent?.trim()).toBe('Connected');
    expect(rows[1].querySelector('.state')?.textContent).toContain('Offline');
    expect(rows[1].querySelector('.meta')?.textContent).toBe('· Last seen 2 h ago');
  });

  it('says so when nothing is paired', async () => {
    const { element } = await render([]);

    expect(element.querySelector('.empty')?.textContent).toContain('No devices yet');
  });

  it('revokes only after a confirming second click', async () => {
    const { fixture, element } = await render([pixel]);
    const revoked: string[] = [];
    fixture.componentInstance.revoke.subscribe((id) => revoked.push(id));
    const button = (label: string) =>
      [...element.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === label);

    button('Revoke')?.click();
    await fixture.whenStable();
    expect(revoked).toEqual([]);

    button('Revoke?')?.click();
    await fixture.whenStable();

    expect(revoked).toEqual(['d1']);
    expect(button('Revoke')).toBeDefined();
  });

  it('can back out of a revoke', async () => {
    const { fixture, element } = await render([pixel]);
    const revoked: string[] = [];
    fixture.componentInstance.revoke.subscribe((id) => revoked.push(id));
    const button = (label: string) =>
      [...element.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === label);

    button('Revoke')?.click();
    await fixture.whenStable();
    button('Keep')?.click();
    await fixture.whenStable();

    expect(revoked).toEqual([]);
    expect(button('Revoke')).toBeDefined();
  });
});
