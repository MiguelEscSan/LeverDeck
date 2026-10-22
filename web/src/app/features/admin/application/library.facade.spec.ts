import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { ConnectionStatus } from '../../../core/connection/domain/connection-status';
import { RequestError } from '../../../core/connection/domain/request-error';
import type { EventMessage, RegisteredAppDto, SettingsDto } from '../../../core/protocol';
import { LibraryFacade } from './library.facade';

const settings = (apps: RegisteredAppDto[]): SettingsDto => ({
  startWithWindows: false,
  defaultGrid: { columns: 5, rows: 3 },
  obs: null,
  apps,
});

/** The agent behind `/admin/ws` for apps and games; `registerApp` pushes `settingsUpdated` before the ack. */
class FakeAgent {
  readonly state = signal<ConnectionStatus>('reconnecting');
  readonly status = this.state.asReadonly();
  readonly connected = computed(() => this.state() === 'connected');
  readonly events = new Subject<EventMessage>();
  readonly commands: { type: string; payload: unknown }[] = [];
  apps: RegisteredAppDto[] = [];
  games = [{ store: 'steam' as const, gameId: '570', name: 'Dota 2' }];
  fail: RequestError | null = null;

  async query(type: string): Promise<unknown> {
    return type === 'getSettings' ? { settings: settings(this.apps) } : { games: this.games };
  }

  async command(type: string, payload: { displayName?: string; path?: string; appId?: string }): Promise<unknown> {
    this.commands.push({ type, payload });
    if (this.fail) {
      throw this.fail;
    }
    if (type === 'registerApp') {
      this.apps = [...this.apps, { appId: 'a1', displayName: payload.displayName!, path: payload.path! }];
      this.events.next({ kind: 'event', type: 'settingsUpdated', payload: { settings: settings(this.apps) } });
      return { kind: 'ack', requestId: 'x', payload: { appId: 'a1' } };
    }
    return { kind: 'ack', requestId: 'x' };
  }
}

describe('LibraryFacade', () => {
  let agent: FakeAgent;
  let library: LibraryFacade;

  const settle = async () => {
    TestBed.tick();
    for (let i = 0; i < 10; i++) {
      await Promise.resolve();
    }
  };

  beforeEach(async () => {
    agent = new FakeAgent();
    agent.apps = [{ appId: 'a0', displayName: 'Notepad', path: 'C:\\Windows\\notepad.exe' }];
    TestBed.configureTestingModule({ providers: [{ provide: AdminConnectionFacade, useValue: agent }] });
    library = TestBed.inject(LibraryFacade);
    agent.state.set('connected');
    await settle();
  });

  it('loads the registered apps and the installed games once connected', () => {
    expect(library.apps().map((app) => app.displayName)).toEqual(['Notepad']);
    expect(library.games()).toEqual([{ store: 'steam', gameId: '570', name: 'Dota 2' }]);
  });

  it('registers an app with a cleaned path and returns its id', async () => {
    const id = await library.registerApp(' OBS ', '"C:\\OBS\\obs64.exe"');

    expect(id).toBe('a1');
    expect(agent.commands).toEqual([
      { type: 'registerApp', payload: { displayName: 'OBS', path: 'C:\\OBS\\obs64.exe' } },
    ]);
    expect(library.apps().map((app) => app.displayName)).toEqual(['Notepad', 'OBS']);
  });

  it('shows why the agent refused an app', async () => {
    agent.fail = new RequestError('validationFailed', 'This executable is already registered.');

    expect(await library.registerApp('Notepad', 'C:\\Windows\\notepad.exe')).toBeNull();
    expect(library.error()).toBe('This executable is already registered.');
  });

  it('explains an app that a key still uses', async () => {
    agent.fail = new RequestError('validationFailed', 'App a0 is still used by a button.');

    await library.unregisterApp('a0');

    expect(library.error()).toBe('A key still launches this app. Change or remove that key first.');
  });

  it('follows settings changes and reads the games again on request', async () => {
    agent.events.next({ kind: 'event', type: 'settingsUpdated', payload: { settings: settings([]) } });
    agent.games = [];
    await library.refreshGames();

    expect(library.apps()).toEqual([]);
    expect(library.games()).toEqual([]);
  });
});
