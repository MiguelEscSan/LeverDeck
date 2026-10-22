import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ConnectionFacade } from '../../../../../core/connection/application/connection.facade';
import type { ConnectionStatus } from '../../../../../core/connection/domain/connection-status';
import { ShellPage } from './shell-page';

describe('ShellPage', () => {
  const status = signal<ConnectionStatus>('reconnecting');

  beforeEach(() => {
    status.set('reconnecting');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ConnectionFacade,
          useValue: { status, connected: computed(() => status() === 'connected') },
        },
      ],
    });
  });

  it('shows the app name', async () => {
    const fixture = TestBed.createComponent(ShellPage);
    await fixture.whenStable();

    const title = (fixture.nativeElement as HTMLElement).querySelector('h1');
    expect(title?.textContent?.trim()).toBe('LeverDeck');
  });

  it('shows the live connection status from the facade', async () => {
    const fixture = TestBed.createComponent(ShellPage);
    await fixture.whenStable();
    const badge = () =>
      (fixture.nativeElement as HTMLElement).querySelector('app-status-badge')?.textContent?.trim();

    expect(badge()).toBe('Reconnecting…');

    status.set('connected');
    await fixture.whenStable();

    expect(badge()).toBe('Connected');
  });
});
