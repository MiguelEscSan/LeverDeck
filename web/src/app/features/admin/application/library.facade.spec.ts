import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { ConnectionStatus } from '../../../core/connection/domain/connection-status';
import { RequestError } from '../../../core/connection/domain/request-error';
import type { EventMessage, RegisteredAppDto, SettingsDto, SoundDto } from '../../../core/protocol';
import { IconEncoder } from './icon-encoder.port';
import { IdSource } from './id-source.port';
import { LibraryFacade } from './library.facade';
import { SoundTransport } from './sound-transport.port';

/** The browser's part of a sound clip: records the uploads and previews, and refuses when told to. */
class FakeSoundTransport extends SoundTransport {
  readonly uploads: { soundId: string; name: string }[] = [];
  readonly previews: string[] = [];
  refuse: string | null = null;

  async upload(soundId: string, name: string): Promise<void> {
    if (this.refuse) {
      throw new Error(this.refuse);
    }
    this.uploads.push({ soundId, name });
  }

  async preview(soundId: string): Promise<void> {
    if (this.refuse) {
      throw new Error(this.refuse);
    }
    this.previews.push(soundId);
  }
}

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
  sounds: SoundDto[] = [{ id: 'sound-00000000000000000000000000000001', name: 'Applause', durationMs: 3000, sizeBytes: 4096 }];
  games = [{ store: 'steam' as const, gameId: '570', name: 'Dota 2' }];
  fail: RequestError | null = null;

  async query(type: string): Promise<unknown> {
    if (type === 'getIcons') {
      return { icons: this.icons };
    }
    if (type === 'getSounds') {
      return { sounds: this.sounds };
    }
    return type === 'getSettings' ? { settings: settings(this.apps) } : { games: this.games };
  }

  async command(
    type: string,
    payload: { displayName?: string; path?: string; appId?: string; iconId?: string; data?: string; soundId?: string; name?: string },
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
    if (type === 'deleteSound') {
      this.sounds = this.sounds.filter((sound) => sound.id !== payload.soundId);
      this.events.next({ kind: 'event', type: 'soundsChanged', payload: { sounds: this.sounds } });
    }
    if (type === 'renameSound') {
      this.sounds = this.sounds.map((sound) => (sound.id === payload.soundId ? { ...sound, name: payload.name! } : sound));
      this.events.next({ kind: 'event', type: 'soundsChanged', payload: { sounds: this.sounds } });
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
  let sounds: FakeSoundTransport;
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
    sounds = new FakeSoundTransport();
    TestBed.configureTestingModule({
      providers: [
        { provide: AdminConnectionFacade, useValue: agent },
        { provide: IdSource, useValue: { next: () => '0123abcd-89ab-4def-8123-456789abcdef' } },
        { provide: IconEncoder, useValue: encoder },
        { provide: SoundTransport, useValue: sounds },
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

  describe('sounds', () => {
    const wav = new File(['RIFF'], 'Applause Short.wav', { type: 'audio/wav' });

    it('loads the sound library once connected', () => {
      expect(library.sounds().map((sound) => sound.name)).toEqual(['Applause']);
    });

    it('uploads a WAV under a new id with its name and lists it again', async () => {
      expect(await library.uploadSound(wav, '  Applause Short  ')).toBe(true);

      expect(sounds.uploads).toEqual([{ soundId: 'sound-0123abcd89ab4def8123456789abcdef', name: 'Applause Short' }]);
      expect(library.soundUploading()).toBe(false);
    });

    it('refuses a file that is not a WAV, without uploading', async () => {
      expect(await library.uploadSound(new File(['ID3'], 'song.mp3'), 'Song')).toBe(false);

      expect(library.error()).toMatch(/Convert MP3, OGG or FLAC to WAV first/);
      expect(sounds.uploads).toEqual([]);
    });

    it('shows the reason the agent gives for a refused upload', async () => {
      sounds.refuse = 'A sound must be an uncompressed WAV file (PCM).';

      expect(await library.uploadSound(wav, 'Applause')).toBe(false);
      expect(library.error()).toBe('A sound must be an uncompressed WAV file (PCM).');
    });

    it('renames a clip and keeps its id', async () => {
      await library.renameSound('sound-00000000000000000000000000000001', 'Crowd');

      expect(agent.commands).toEqual([
        { type: 'renameSound', payload: { soundId: 'sound-00000000000000000000000000000001', name: 'Crowd' } },
      ]);
      expect(library.sounds()[0]).toEqual(expect.objectContaining({ id: 'sound-00000000000000000000000000000001', name: 'Crowd' }));
    });

    it('does not send an empty name', async () => {
      await library.renameSound('sound-00000000000000000000000000000001', '   ');

      expect(agent.commands).toEqual([]);
      expect(library.error()).toBe('Use 1–60 characters.');
    });

    it('deletes a clip and drops it from the list', async () => {
      await library.deleteSound('sound-00000000000000000000000000000001');

      expect(library.sounds()).toEqual([]);
    });

    it('explains a clip that a key still plays', async () => {
      agent.fail = new RequestError('validationFailed', 'Sound sound-0000 is still played by a key.');

      await library.deleteSound('sound-00000000000000000000000000000001');

      expect(library.error()).toBe('A key still plays this sound. Change that key first.');
    });

    it('plays a preview through the transport and shows a refusal', async () => {
      await library.previewSound('sound-00000000000000000000000000000001');
      expect(sounds.previews).toEqual(['sound-00000000000000000000000000000001']);

      sounds.refuse = "Couldn't load the sound. It may have been removed.";
      await library.previewSound('sound-00000000000000000000000000000001');
      expect(library.error()).toBe("Couldn't load the sound. It may have been removed.");
    });

    it('follows soundsChanged', () => {
      agent.events.next({ kind: 'event', type: 'soundsChanged', payload: { sounds: [] } });

      expect(library.sounds()).toEqual([]);
    });
  });
});
