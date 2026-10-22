import { TestBed } from '@angular/core/testing';
import type { ConnectionStatus } from '../../../../../core/connection/domain/connection-status';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  const render = async (status: ConnectionStatus) => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('status', status);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  };

  it.each([
    ['connected', 'Connected'],
    ['reconnecting', 'Reconnecting…'],
    ['offline', 'Offline'],
  ] as const)('shows %s as "%s"', async (status, label) => {
    const element = await render(status);

    expect(element.textContent?.trim()).toBe(label);
    expect(element.querySelector('[data-status]')?.getAttribute('data-status')).toBe(status);
  });

  it('announces changes politely to assistive technology', async () => {
    const element = await render('offline');

    const live = element.querySelector('[role="status"]');
    expect(live?.getAttribute('aria-live')).toBe('polite');
  });
});
