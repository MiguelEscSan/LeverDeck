import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { ConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { ConnectionStatus } from '../../../core/connection/domain/connection-status';
import { RequestError } from '../../../core/connection/domain/request-error';
import type { ButtonDto, EventMessage, ProfileDto, ProfileSummaryDto } from '../../../core/protocol';
import { DeckFacade, FLASH_MS } from './deck.facade';
import { DeckPreferences } from './deck-preferences.port';
import { Haptics } from './haptics.port';

const key = (id: string, column: number, action: ButtonDto['action']): ButtonDto => ({
  id,
  row: 0,
  column,
  label: id,
  color: 'dark',
  iconId: id,
  action,
});

const media = key('play', 0, { type: 'mediaKey', key: 'playPause' });
const next = key('next', 1, { type: 'changePage', target: 'next', pageIndex: null });
const toGames = key('games', 2, { type: 'switchProfile', profileId: 'p2' });

const defaultProfile: ProfileDto = {
  id: 'p1',
  name: 'Default',
  color: 'teal',
  grid: { columns: 3, rows: 1 },
  pages: [
    { index: 0, buttons: [media, next, toGames] },
    { index: 1, buttons: [] },
  ],
};

const games: ProfileDto = {
  id: 'p2',
  name: 'Games',
  color: 'red',
  grid: { columns: 5, rows: 3 },
  pages: [{ index: 0, buttons: [] }],
};

const summaries: ProfileSummaryDto[] = [
  { id: 'p1', name: 'Default', color: 'teal' },
  { id: 'p2', name: 'Games', color: 'red' },
];

/** The device connection as the deck sees it: queries answered from a table, commands recorded. */
class FakeConnection {
  readonly state = signal<ConnectionStatus>('reconnecting');
  readonly status = this.state.asReadonly();
  readonly connected = computed(() => this.state() === 'connected');
  readonly rejection = signal(null).asReadonly();
  readonly events = new Subject<EventMessage>();
  readonly commands: { type: string; payload: unknown }[] = [];
  readonly profiles = new Map<string, ProfileDto>([
    ['p1', defaultProfile],
    ['p2', games],
  ]);
  list = summaries;
  commandResult: () => Promise<unknown> = async () => ({ kind: 'ack', requestId: 'x' });
  starts = 0;

  start(): boolean {
    this.starts++;
    return true;
  }

  async query(type: string, payload: { profileId?: string }): Promise<unknown> {
    if (type === 'getProfiles') {
      return { profiles: this.list };
    }
    const profile = this.profiles.get(payload.profileId ?? '');
    if (!profile) {
      throw new RequestError('notFound');
    }
    return { profile };
  }

  command(type: string, payload: unknown): Promise<unknown> {
    this.commands.push({ type, payload });
    return this.commandResult();
  }
}

class MemoryPreferences extends DeckPreferences {
  constructor(public profileId: string | null = null) {
    super();
  }

  lastProfileId(): string | null {
    return this.profileId;
  }

  rememberProfile(profileId: string): void {
    this.profileId = profileId;
  }
}

class FakeHaptics extends Haptics {
  readonly pulses: string[] = [];

  tap(): void {
    this.pulses.push('tap');
  }

  failure(): void {
    this.pulses.push('failure');
  }
}

describe('DeckFacade', () => {
  let connection: FakeConnection;
  let preferences: MemoryPreferences;
  let haptics: FakeHaptics;
  let facade: DeckFacade;

  const settle = async () => {
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(0);
  };

  const setUp = async (rememberedProfile: string | null = null) => {
    vi.useFakeTimers();
    connection = new FakeConnection();
    preferences = new MemoryPreferences(rememberedProfile);
    haptics = new FakeHaptics();
    TestBed.configureTestingModule({
      providers: [
        { provide: ConnectionFacade, useValue: connection },
        { provide: DeckPreferences, useValue: preferences },
        { provide: Haptics, useValue: haptics },
      ],
    });
    facade = TestBed.inject(DeckFacade);
    connection.state.set('connected');
    await settle();
  };

  afterEach(() => vi.useRealTimers());

  describe('loading', () => {
    it('loads the profile list and opens the first profile once connected', async () => {
      await setUp();

      expect(facade.profiles()).toEqual(summaries);
      expect(facade.profile()?.id).toBe('p1');
      expect(facade.page()).toBe(0);
      expect(facade.cells().map((cell) => cell.button?.id)).toEqual(['play', 'next', 'games']);
    });

    it('opens the remembered profile', async () => {
      await setUp('p2');

      expect(facade.profile()?.id).toBe('p2');
    });

    it('falls back to the first profile when the remembered one is gone', async () => {
      await setUp('deleted');

      expect(facade.profile()?.id).toBe('p1');
    });

    it('reloads after a reconnect', async () => {
      await setUp();
      connection.list = [summaries[0]];

      connection.state.set('reconnecting');
      await settle();
      connection.state.set('connected');
      await settle();

      expect(facade.profiles()).toEqual([summaries[0]]);
    });

    it('starts the connection', async () => {
      await setUp();

      facade.start();

      expect(connection.starts).toBe(1);
    });
  });

  describe('pressing keys', () => {
    it('sends executeAction for an agent action and flashes success', async () => {
      await setUp();

      const press = facade.press(media);
      expect(facade.feedback()['play']).toBe('pending');
      await press;

      expect(connection.commands).toEqual([
        { type: 'executeAction', payload: { profileId: 'p1', pageIndex: 0, buttonId: 'play' } },
      ]);
      expect(facade.feedback()['play']).toBe('success');
      expect(haptics.pulses).toEqual(['tap']);

      vi.advanceTimersByTime(FLASH_MS);
      expect(facade.feedback()['play']).toBeUndefined();
    });

    it('flashes failure and buzzes when the action fails', async () => {
      await setUp();
      connection.commandResult = () => Promise.reject(new RequestError('actionFailed'));

      await facade.press(media);

      expect(facade.feedback()['play']).toBe('failure');
      expect(haptics.pulses).toEqual(['tap', 'failure']);
    });

    it('changes the page on the device without asking the agent', async () => {
      await setUp();

      await facade.press(next);

      expect(facade.page()).toBe(1);
      expect(connection.commands).toEqual([]);
    });

    it('switches the profile on the device and remembers it', async () => {
      await setUp();

      await facade.press(toGames);

      expect(facade.profile()?.id).toBe('p2');
      expect(preferences.profileId).toBe('p2');
      expect(connection.commands).toEqual([]);
    });

    it('sends the page index of the current page', async () => {
      await setUp();
      const onSecondPage = key('stop', 0, { type: 'mediaKey', key: 'stop' });
      connection.events.next({
        kind: 'event',
        type: 'profileUpdated',
        payload: {
          profile: { ...defaultProfile, pages: [defaultProfile.pages[0], { index: 1, buttons: [onSecondPage] }] },
        },
      });
      facade.goToPage(1);

      await facade.press(onSecondPage);

      expect(connection.commands[0].payload).toEqual({ profileId: 'p1', pageIndex: 1, buttonId: 'stop' });
    });
  });

  describe('selecting', () => {
    it('opens a profile from the picker and starts on its first page', async () => {
      await setUp();
      facade.goToPage(1);

      await facade.selectProfile('p2');

      expect(facade.profile()?.id).toBe('p2');
      expect(facade.page()).toBe(0);
      expect(preferences.profileId).toBe('p2');
    });

    it('ignores pages that do not exist', async () => {
      await setUp();

      facade.goToPage(9);

      expect(facade.page()).toBe(0);
    });
  });

  describe('events', () => {
    it('shows the new layout when the active profile is updated', async () => {
      await setUp();
      facade.goToPage(1);
      const updated: ProfileDto = { ...defaultProfile, name: 'Streaming', pages: [defaultProfile.pages[0]] };

      connection.events.next({ kind: 'event', type: 'profileUpdated', payload: { profile: updated } });

      expect(facade.profile()?.name).toBe('Streaming');
      expect(facade.page()).toBe(0);
      expect(facade.profiles()[0]).toEqual({ id: 'p1', name: 'Streaming', color: 'teal' });
    });

    it('adds a new profile to the list without leaving the current one', async () => {
      await setUp();
      const created: ProfileDto = { ...games, id: 'p3', name: 'Media' };

      connection.events.next({ kind: 'event', type: 'profileUpdated', payload: { profile: created } });

      expect(facade.profiles().map((profile) => profile.id)).toEqual(['p1', 'p2', 'p3']);
      expect(facade.profile()?.id).toBe('p1');
    });

    it('moves to the first remaining profile when the active one is deleted', async () => {
      await setUp('p2');

      connection.events.next({ kind: 'event', type: 'profileDeleted', payload: { profileId: 'p2' } });
      await settle();

      expect(facade.profiles().map((profile) => profile.id)).toEqual(['p1']);
      expect(facade.profile()?.id).toBe('p1');
    });
  });
});
