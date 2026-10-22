import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { ConnectionStatus } from '../../../core/connection/domain/connection-status';
import { RequestError } from '../../../core/connection/domain/request-error';
import type { ButtonDto, EventMessage, ProfileDto } from '../../../core/protocol';
import { findButton, summaryOf } from '../domain/profile-editing';
import { IdSource } from './id-source.port';
import { ProfileEditorFacade } from './profile-editor.facade';

const key = (id: string, row: number, column: number, overrides: Partial<ButtonDto> = {}): ButtonDto => ({
  id,
  row,
  column,
  label: '',
  color: 'dark',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
  ...overrides,
});

const streaming = (): ProfileDto => ({
  id: 'p1',
  name: 'Streaming',
  color: 'red',
  grid: { columns: 5, rows: 3 },
  pages: [{ index: 0, buttons: [key('b1', 0, 0)] }],
});

const games = (): ProfileDto => ({
  id: 'p2',
  name: 'Games',
  color: 'purple',
  grid: { columns: 3, rows: 3 },
  pages: [{ index: 0, buttons: [] }],
});

/** The agent behind `/admin/ws`: stores profiles and pushes events before the ack, as the real one does. */
class FakeAgent {
  readonly state = signal<ConnectionStatus>('reconnecting');
  readonly status = this.state.asReadonly();
  readonly connected = computed(() => this.state() === 'connected');
  readonly events = new Subject<EventMessage>();
  readonly saved: ProfileDto[] = [];
  profiles = new Map<string, ProfileDto>([
    ['p1', streaming()],
    ['p2', games()],
  ]);
  fail: RequestError | null = null;

  async query(type: string, payload: { profileId?: string }): Promise<unknown> {
    switch (type) {
      case 'getProfiles':
        return { profiles: [...this.profiles.values()].map(summaryOf) };
      case 'getProfile':
        return { profile: structuredClone(this.profiles.get(payload.profileId ?? '')) };
      case 'getSettings':
        return { settings: { startWithWindows: false, defaultGrid: { columns: 8, rows: 4 }, obs: null, apps: [] } };
      default:
        throw new Error(`Unexpected query ${type}`);
    }
  }

  async command(type: string, payload: { profile?: ProfileDto; profileId?: string }): Promise<unknown> {
    if (this.fail) {
      throw this.fail;
    }
    if (type === 'saveProfile' && payload.profile) {
      const profile = structuredClone(payload.profile);
      this.saved.push(profile);
      this.profiles.set(profile.id, profile);
      this.events.next({ kind: 'event', type: 'profileUpdated', payload: { profile } });
    }
    if (type === 'deleteProfile' && payload.profileId) {
      this.profiles.delete(payload.profileId);
      this.events.next({ kind: 'event', type: 'profileDeleted', payload: { profileId: payload.profileId } });
    }
    return { kind: 'ack', requestId: 'x' };
  }
}

class SequentialIds extends IdSource {
  private n = 0;

  next(): string {
    return `id-${++this.n}`;
  }
}

describe('ProfileEditorFacade', () => {
  let agent: FakeAgent;
  let editor: ProfileEditorFacade;

  const settle = async () => {
    TestBed.tick();
    for (let i = 0; i < 10; i++) {
      await Promise.resolve();
    }
    TestBed.tick();
  };

  beforeEach(async () => {
    agent = new FakeAgent();
    TestBed.configureTestingModule({
      providers: [
        { provide: AdminConnectionFacade, useValue: agent },
        { provide: IdSource, useClass: SequentialIds },
      ],
    });
    editor = TestBed.inject(ProfileEditorFacade);
    agent.state.set('connected');
    await settle();
  });

  it('loads the profiles once connected and opens the first one', () => {
    expect(editor.profiles().map((profile) => profile.name)).toEqual(['Streaming', 'Games']);
    expect(editor.profile()?.id).toBe('p1');
    expect(editor.cells()).toHaveLength(15);
    expect(editor.cells()[0].button?.id).toBe('b1');
  });

  it('opens another profile', async () => {
    await editor.selectProfile('p2');

    expect(editor.profile()?.name).toBe('Games');
    expect(editor.cells()).toHaveLength(9);
  });

  describe('keys', () => {
    it('selects a key or an empty position', () => {
      editor.selectCell(0, 0);
      expect(editor.selection()).toEqual({ kind: 'key', button: key('b1', 0, 0) });

      editor.selectCell(1, 2);
      expect(editor.selection()).toEqual({ kind: 'empty', row: 1, column: 2 });
    });

    it('adds a key on the selected empty position and saves the profile', async () => {
      editor.selectCell(1, 2);
      editor.addKey();
      await settle();

      expect(editor.selection()).toMatchObject({ kind: 'key', button: { id: 'id-1', row: 1, column: 2 } });
      expect(findButton(agent.profiles.get('p1')!, 'id-1')).not.toBeNull();
    });

    it('edits the selected key and saves each change', async () => {
      editor.selectCell(0, 0);
      editor.updateKey({ label: 'Play', color: 'blue' });
      await settle();

      expect(agent.saved).toHaveLength(1);
      expect(findButton(agent.profiles.get('p1')!, 'b1')).toMatchObject({ label: 'Play', color: 'blue' });
    });

    it('lets an untouched icon follow the action, but keeps a chosen one', async () => {
      editor.selectCell(0, 0);
      editor.changeAction({ type: 'mediaKey', key: 'mute' });
      expect(findButton(editor.profile()!, 'b1')?.iconId).toBe('volume-mute');

      editor.updateKey({ iconId: 'star' });
      editor.changeAction({ type: 'hotkey', modifiers: ['ctrl'], key: 'm' });
      await settle();

      expect(findButton(agent.profiles.get('p1')!, 'b1')).toMatchObject({
        iconId: 'star',
        action: { type: 'hotkey', modifiers: ['ctrl'], key: 'm' },
      });
    });

    it('labels a key after the app or game it launches until the owner labels it', async () => {
      editor.selectCell(1, 1);
      editor.addKey();
      editor.changeAction({ type: 'launchGame', store: 'steam', gameId: '1091500' }, 'Cyberpunk 2077: Phantom');
      expect(editor.selection()).toMatchObject({
        kind: 'key',
        button: { label: 'Cyberpunk 20', iconId: 'gamepad', action: { type: 'launchGame', gameId: '1091500' } },
      });

      editor.changeAction({ type: 'launchApp', appId: 'a1' }, 'OBS Studio', 'Cyberpunk 2077: Phantom');
      expect(editor.selection()).toMatchObject({ kind: 'key', button: { label: 'OBS Studio', iconId: 'window' } });

      editor.updateKey({ label: 'Stream' });
      editor.changeAction({ type: 'launchApp', appId: 'a2' }, 'Discord', 'OBS Studio');
      await settle();

      expect(editor.selection()).toMatchObject({ kind: 'key', button: { label: 'Stream' } });
    });

    it('removes the selected key and leaves its position selected', async () => {
      editor.selectCell(0, 0);
      editor.removeKey();
      await settle();

      expect(editor.selection()).toEqual({ kind: 'empty', row: 0, column: 0 });
      expect(agent.profiles.get('p1')!.pages[0].buttons).toEqual([]);
    });

    it('refuses a too-long label without saving', () => {
      editor.selectCell(0, 0);
      editor.updateKey({ label: 'x'.repeat(13) });

      expect(editor.error()).toBe('Use at most 12 characters.');
      expect(agent.saved).toEqual([]);
    });
  });

  describe('pages and grid', () => {
    it('adds a page and goes to it, then removes it', async () => {
      editor.addPage();
      await settle();
      expect(editor.pageCount()).toBe(2);
      expect(editor.page()).toBe(1);

      editor.removePage();
      await settle();
      expect(editor.pageCount()).toBe(1);
      expect(editor.page()).toBe(0);
      expect(agent.profiles.get('p1')!.pages).toHaveLength(1);
    });

    it('changes the grid unless keys would fall outside', async () => {
      editor.changeGrid({ columns: 8, rows: 4 });
      await settle();
      expect(agent.profiles.get('p1')!.grid).toEqual({ columns: 8, rows: 4 });

      editor.selectCell(3, 7);
      editor.addKey();
      editor.changeGrid({ columns: 3, rows: 3 });

      expect(editor.error()).toBe('One key is outside 3 × 3. Move or remove it first.');
      expect(editor.profile()?.grid).toEqual({ columns: 8, rows: 4 });
    });
  });

  describe('profiles', () => {
    it('creates a profile with a free name and color on the default grid, and opens it', async () => {
      editor.createProfile();
      await settle();

      expect(editor.profile()).toEqual({
        id: 'id-1',
        name: 'New profile',
        color: 'blue',
        grid: { columns: 8, rows: 4 },
        pages: [{ index: 0, buttons: [] }],
      });
      expect(agent.profiles.has('id-1')).toBe(true);
      expect(editor.profiles()).toHaveLength(3);
    });

    it('renames and recolors, refusing a taken name', async () => {
      editor.renameProfile('games');
      expect(editor.error()).toBe('A profile named "games" already exists.');

      editor.renameProfile(' Live ');
      editor.recolorProfile('teal');
      await settle();

      expect(agent.profiles.get('p1')).toMatchObject({ name: 'Live', color: 'teal' });
      expect(editor.profiles()[0]).toEqual({ id: 'p1', name: 'Live', color: 'teal' });
    });

    it('deletes the open profile and opens the next one', async () => {
      await editor.deleteProfile();
      await settle();

      expect(editor.profiles().map((profile) => profile.id)).toEqual(['p2']);
      expect(editor.profile()?.id).toBe('p2');
      expect(editor.canDeleteProfile()).toBe(false);
    });
  });

  describe('sync', () => {
    it('takes a change made elsewhere while idle', async () => {
      const elsewhere = { ...streaming(), name: 'Changed in another tab' };
      agent.events.next({ kind: 'event', type: 'profileUpdated', payload: { profile: elsewhere } });

      expect(editor.profile()?.name).toBe('Changed in another tab');
    });

    it('undoes a change the agent refused and says why', async () => {
      agent.fail = new RequestError('validationFailed', 'Label must be at most 12 characters.');
      editor.selectCell(0, 0);
      editor.updateKey({ label: 'Play' });
      expect(findButton(editor.profile()!, 'b1')?.label).toBe('Play');
      await settle();

      expect(findButton(editor.profile()!, 'b1')?.label).toBe('');
      expect(editor.error()).toBe('Label must be at most 12 characters.');
      expect(editor.saving()).toBe(false);
    });

    it('drops a new profile the agent refused', async () => {
      agent.fail = new RequestError('timeout');
      editor.createProfile();
      await settle();
      await settle();

      expect(editor.profiles().map((profile) => profile.id)).toEqual(['p1', 'p2']);
      expect(editor.profile()?.id).toBe('p1');
      expect(editor.error()).toBe("Couldn't save the change. It was undone.");
    });
  });
});
