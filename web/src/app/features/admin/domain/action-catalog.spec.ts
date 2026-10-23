import type { InstalledGameDto, ProfileSummaryDto, RegisteredAppDto, SoundDto } from '../../../core/protocol';
import {
  type ActionContext,
  actionSummary,
  defaultAction,
  gameIdError,
  isEditableAction,
  parseUrl,
  suggestedIcon,
  toggleModifier,
  VIRTUAL_KEY_NAMES,
  VIRTUAL_KEYS,
} from './action-catalog';

const profiles: ProfileSummaryDto[] = [
  { id: 'p1', name: 'Streaming', color: 'red' },
  { id: 'p2', name: 'Games', color: 'purple' },
];
const apps: RegisteredAppDto[] = [{ appId: 'a1', displayName: 'OBS', path: 'C:\\OBS\\obs64.exe' }];
const games: InstalledGameDto[] = [{ store: 'steam', gameId: '570', name: 'Dota 2' }];
const sounds: SoundDto[] = [{ id: 'sound-0123456789abcdef0123456789abcdef', name: 'Applause', durationMs: 3000, sizeBytes: 4096 }];
const context: ActionContext = { profileId: 'p1', profiles, apps, games, sounds };
const empty: ActionContext = { profileId: 'p1', profiles: [profiles[0]], apps: [], games: [] };

describe('actionSummary', () => {
  it('describes each action in one line', () => {
    expect(actionSummary({ type: 'hotkey', modifiers: ['ctrl', 'shift'], key: 'm' }, context)).toBe('Ctrl + Shift + M');
    expect(actionSummary({ type: 'hotkey', modifiers: [], key: 'f13' }, context)).toBe('F13');
    expect(actionSummary({ type: 'mediaKey', key: 'volumeUp' }, context)).toBe('Volume up');
    expect(actionSummary({ type: 'changePage', target: 'next', pageIndex: null }, context)).toBe('Next page');
    expect(actionSummary({ type: 'changePage', target: 'specific', pageIndex: 1 }, context)).toBe('Go to page 2');
    expect(actionSummary({ type: 'switchProfile', profileId: 'p2' }, context)).toBe('Switch to Games');
    expect(actionSummary({ type: 'switchProfile', profileId: 'gone' }, context)).toBe('Switch to a missing profile');
    expect(actionSummary({ type: 'obsScene', sceneName: 'Just Chatting' }, context)).toBe('OBS scene Just Chatting');
    expect(actionSummary({ type: 'launchApp', appId: 'a1' }, context)).toBe('Launch OBS');
    expect(actionSummary({ type: 'launchApp', appId: 'gone' }, context)).toBe('Launch an app that is no longer registered');
    expect(actionSummary({ type: 'launchGame', store: 'steam', gameId: '570' }, context)).toBe('Play Dota 2 (Steam)');
    expect(actionSummary({ type: 'launchGame', store: 'epic', gameId: 'Sugar' }, context)).toBe('Play Sugar (Epic Games)');
    expect(actionSummary({ type: 'openUrl', url: 'https://example.com/' }, context)).toBe('Open https://example.com/');
    expect(actionSummary({ type: 'playSound', soundId: 'sound-0123456789abcdef0123456789abcdef' }, context)).toBe('Play Applause');
    expect(actionSummary({ type: 'playSound', soundId: 'sound-ffffffffffffffffffffffffffffffff' }, context)).toBe(
      'Play a sound that is no longer in the library',
    );
  });
});

describe('defaultAction', () => {
  it('gives each editable type a valid starting action', () => {
    expect(defaultAction('hotkey', context)).toEqual({ type: 'hotkey', modifiers: ['ctrl'], key: 'c' });
    expect(defaultAction('mediaKey', context)).toEqual({ type: 'mediaKey', key: 'playPause' });
    expect(defaultAction('changePage', context)).toEqual({ type: 'changePage', target: 'next', pageIndex: null });
    expect(defaultAction('launchApp', context)).toEqual({ type: 'launchApp', appId: 'a1' });
    expect(defaultAction('launchGame', context)).toEqual({ type: 'launchGame', store: 'steam', gameId: '570' });
  });

  it('switches to another profile when there is one, else to its own', () => {
    expect(defaultAction('switchProfile', context)).toEqual({ type: 'switchProfile', profileId: 'p2' });
    expect(defaultAction('switchProfile', empty)).toEqual({ type: 'switchProfile', profileId: 'p1' });
    expect(defaultAction('playSound', context)).toEqual({ type: 'playSound', soundId: 'sound-0123456789abcdef0123456789abcdef' });
    expect(defaultAction('playSound', empty)).toBeNull();
  });

  it('needs input first for a link, and for an app or game when there is none to pick', () => {
    expect(defaultAction('openUrl', context)).toBeNull();
    expect(defaultAction('launchApp', empty)).toBeNull();
    expect(defaultAction('launchGame', empty)).toBeNull();
  });
});

describe('toggleModifier', () => {
  it('keeps modifiers unique and in display order', () => {
    expect(toggleModifier(['shift'], 'ctrl', true)).toEqual(['ctrl', 'shift']);
    expect(toggleModifier(['ctrl', 'shift'], 'ctrl', false)).toEqual(['shift']);
    expect(toggleModifier(['ctrl'], 'ctrl', true)).toEqual(['ctrl']);
  });
});

describe('catalog', () => {
  it('lists every virtual key once', () => {
    expect(VIRTUAL_KEYS).toHaveLength(92);
    expect(new Set(VIRTUAL_KEYS).size).toBe(92);
  });

  it('names the numeric keypad keys apart from the main ones', () => {
    expect(VIRTUAL_KEY_NAMES.numPadAdd).toBe('Numpad +');
    expect(VIRTUAL_KEY_NAMES.numPadSubtract).toBe('Numpad -');
    expect(VIRTUAL_KEY_NAMES.numPad7).toBe('Numpad 7');
    expect(VIRTUAL_KEY_NAMES.d7).toBe('7');
  });

  it('tells editable actions apart', () => {
    expect(isEditableAction({ type: 'mediaKey', key: 'mute' })).toBe(true);
    expect(isEditableAction({ type: 'openUrl', url: 'https://example.com' })).toBe(true);
    expect(isEditableAction({ type: 'obsScene', sceneName: 'Live' })).toBe(false);
  });

  it('suggests a fitting icon', () => {
    expect(suggestedIcon({ type: 'mediaKey', key: 'mute' })).toBe('volume-mute');
    expect(suggestedIcon({ type: 'changePage', target: 'previous', pageIndex: null })).toBe('page-previous');
    expect(suggestedIcon({ type: 'switchProfile', profileId: 'p2' })).toBe('layers');
    expect(suggestedIcon({ type: 'openUrl', url: 'https://example.com/' })).toBe('globe');
    expect(suggestedIcon({ type: 'launchApp', appId: 'a1' })).toBe('window');
    expect(suggestedIcon({ type: 'launchGame', store: 'epic', gameId: 'Sugar' })).toBe('gamepad');
  });
});

describe('parseUrl', () => {
  it('accepts http and https links and adds https to a bare address', () => {
    expect(parseUrl(' https://example.com/a b ')).toEqual({ url: 'https://example.com/a%20b' });
    expect(parseUrl('http://192.168.1.5:8080')).toEqual({ url: 'http://192.168.1.5:8080/' });
    expect(parseUrl('youtube.com/watch?v=1')).toEqual({ url: 'https://youtube.com/watch?v=1' });
  });

  it('refuses empty, invalid, other schemes and overlong links', () => {
    expect(parseUrl('  ')).toEqual({ error: 'Type the address of the page.' });
    expect(parseUrl('https://')).toEqual({ error: 'That is not a valid address.' });
    expect(parseUrl('file:///C:/secret.txt')).toEqual({ error: 'Only http and https links can be opened.' });
    expect(parseUrl('javascript:alert(1)')).toEqual({ error: 'Only http and https links can be opened.' });
    expect(parseUrl(`https://example.com/${'x'.repeat(2048)}`)).toEqual({ error: 'Use at most 2048 characters.' });
  });
});

describe('gameIdError', () => {
  it('follows the agent rules per store', () => {
    expect(gameIdError('steam', '570')).toBeNull();
    expect(gameIdError('steam', '57a')).toBe('A Steam app id is 1–10 digits, like 570.');
    expect(gameIdError('steam', '12345678901')).not.toBeNull();
    expect(gameIdError('epic', 'Fortnite')).toBeNull();
    expect(gameIdError('epic', 'has:colon')).toBe('An Epic app name is 1–64 letters or digits.');
    expect(gameIdError('epic', '')).not.toBeNull();
  });
});
