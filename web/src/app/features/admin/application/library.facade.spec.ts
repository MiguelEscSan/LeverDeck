import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { ConnectionStatus } from '../../../core/connection/domain/connection-status';
import { RequestError } from '../../../core/connection/domain/request-error';
import type { EventMessage, RegisteredAppDto, SettingsDto } from '../../../core/protocol';
import { IconEncoder } from './icon-encoder.port';
import { IdSource } from './id-source.port';
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
  icons: string[] = ['custom-00000000000000000000000000000001'];
  games = [{ store: 'steam' as const, gameId: '570', name: 'Dota 2' }];
  fail: RequestError | null = null;

  async query(type: string): Promise<unknown> {
    if (type === 'getIcons') {
      return { icons: this.icons };
    }
    return type === 'getSettings' ? { settings: settings(this.apps) } : { games: this.games };
  }

  async command(
    type: string,
    payload: { displayName?: string; path?: string; appId?: string; iconId?: string; data?: string },
  ): Promise<unknown> {
    this.commands.push({ type, payload });
    if (this.fail) {
      throw this.fail;
    }
    if (type === 'registerApp') {
      this.apps = [...this.apps, { appId: 'a1', displayName: payload.displayName!, path: payload.path! }];
      this.events.next({ kind: 'event', type: 'settingsUpdated', payload: { settings: settings(this.apps) } });
      return { kind: 'ack', requestId: 'x', payload: { appId: 'a1' } };
    }
    if (type === 'saveIcon') {
      this.icons = [...this.icons, payload.iconId!];
      this.events.next({ kind: 'event', type: 'iconsChanged', payload: { icons: this.icons } });
    }
    return { kind: 'ack', requestId: 'x' };
  }
}

describe('LibraryFacade', () => {
  let agent: FakeAgent;
  let library: LibraryFacade;
  const encoder = {
    fail: null as string | null,
    async encode(): Promise<string> {
      if (encoder.fail) {
        throw new Error(encoder.fail);
      }
      return 'iVBORw==';
    },
  };

  const settle = async () => {
    TestBed.tick();
    for (let i = 0; i < 10; i++) {
      await Promise.resolve();
    }
  };

  beforeEach(async () => {
    agent = new FakeAgent();
    encoder.fail = null;
    agent.apps = [{ appId: 'a0', displayName: 'Notepad', path: 'C:\\Windows\\notepad.exe' }];
    TestBed.configureTestingModule({
      providers: [
        { provide: AdminConnectionFacade, useValue: agent },
        { provide: IdSource, useValue: { next: () => '0123abcd-89ab-4def-8123-456789abcdef' } },
        { provide: IconEncoder, useValue: encoder },
      ],
    });
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

  describe('icons', () => {
    it('loads the uploaded icons once connected', () => {
      expect(library.icons()).toEqual(['custom-00000000000000000000000000000001']);
    });

    it('encodes a picture, uploads it under a new id and lists it', async () => {
      const id = await library.uploadIcon(new Blob(['picture']));

      expect(id).toBe('custom-0123abcd89ab4def8123456789abcdef');
      expect(agent.commands).toEqual([
        { type: 'saveIcon', payload: { iconId: 'custom-0123abcd89ab4def8123456789abcdef', data: 'iVBORw==' } },
      ]);
      expect(library.icons()).toContain('custom-0123abcd89ab4def8123456789abcdef');
    });

    it('shows why a picture could not be read, without uploading', async () => {
      encoder.fail = "That file isn't a picture this browser can read.";

      expect(await library.uploadIcon(new Blob(['x']))).toBeNull();
      expect(library.error()).toBe("That file isn't a picture this browser can read.");
      expect(agent.commands).toEqual([]);
    });

    it('explains an icon that a key still shows', async () => {
      agent.fail = new RequestError('validationFailed', 'Icon x is still used by a key.');

      await library.deleteIcon('custom-00000000000000000000000000000001');

      expect(library.error()).toBe('A key still shows this icon. Give that key another icon first.');
    });

    it('follows iconsChanged', () => {
      agent.events.next({ kind: 'event', type: 'iconsChanged', payload: { icons: [] } });

      expect(library.icons()).toEqual([]);
    });
  });
});
