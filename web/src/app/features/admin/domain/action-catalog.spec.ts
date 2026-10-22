import type { ProfileSummaryDto } from '../../../core/protocol';
import {
  actionSummary,
  defaultAction,
  isEditableAction,
  suggestedIcon,
  toggleModifier,
  VIRTUAL_KEYS,
} from './action-catalog';

const profiles: ProfileSummaryDto[] = [
  { id: 'p1', name: 'Streaming', color: 'red' },
  { id: 'p2', name: 'Games', color: 'purple' },
];

describe('actionSummary', () => {
  it('describes each action in one line', () => {
    expect(actionSummary({ type: 'hotkey', modifiers: ['ctrl', 'shift'], key: 'm' }, profiles)).toBe('Ctrl + Shift + M');
    expect(actionSummary({ type: 'hotkey', modifiers: [], key: 'f13' }, profiles)).toBe('F13');
    expect(actionSummary({ type: 'mediaKey', key: 'volumeUp' }, profiles)).toBe('Volume up');
    expect(actionSummary({ type: 'changePage', target: 'next', pageIndex: null }, profiles)).toBe('Next page');
    expect(actionSummary({ type: 'changePage', target: 'specific', pageIndex: 1 }, profiles)).toBe('Go to page 2');
    expect(actionSummary({ type: 'switchProfile', profileId: 'p2' }, profiles)).toBe('Switch to Games');
    expect(actionSummary({ type: 'switchProfile', profileId: 'gone' }, profiles)).toBe('Switch to a missing profile');
    expect(actionSummary({ type: 'obsScene', sceneName: 'Just Chatting' }, profiles)).toBe('OBS scene Just Chatting');
  });
});

describe('defaultAction', () => {
  it('gives each editable type a valid starting action', () => {
    expect(defaultAction('hotkey', 'p1', profiles)).toEqual({ type: 'hotkey', modifiers: ['ctrl'], key: 'c' });
    expect(defaultAction('mediaKey', 'p1', profiles)).toEqual({ type: 'mediaKey', key: 'playPause' });
    expect(defaultAction('changePage', 'p1', profiles)).toEqual({ type: 'changePage', target: 'next', pageIndex: null });
  });

  it('switches to another profile when there is one, else to its own', () => {
    expect(defaultAction('switchProfile', 'p1', profiles)).toEqual({ type: 'switchProfile', profileId: 'p2' });
    expect(defaultAction('switchProfile', 'p1', [profiles[0]])).toEqual({ type: 'switchProfile', profileId: 'p1' });
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
    expect(VIRTUAL_KEYS).toHaveLength(76);
    expect(new Set(VIRTUAL_KEYS).size).toBe(76);
  });

  it('tells editable actions apart', () => {
    expect(isEditableAction({ type: 'mediaKey', key: 'mute' })).toBe(true);
    expect(isEditableAction({ type: 'openUrl', url: 'https://example.com' })).toBe(false);
  });

  it('suggests a fitting icon', () => {
    expect(suggestedIcon({ type: 'mediaKey', key: 'mute' })).toBe('volume-mute');
    expect(suggestedIcon({ type: 'changePage', target: 'previous', pageIndex: null })).toBe('page-previous');
    expect(suggestedIcon({ type: 'switchProfile', profileId: 'p2' })).toBe('layers');
  });
});
