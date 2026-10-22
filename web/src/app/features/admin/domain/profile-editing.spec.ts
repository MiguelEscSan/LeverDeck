import type { ButtonDto, ProfileDto, ProfileSummaryDto } from '../../../core/protocol';
import {
  addPage,
  buttonAt,
  changeGrid,
  type EditResult,
  findButton,
  MAX_PAGES,
  newProfile,
  nextProfileColor,
  nextProfileName,
  placeButton,
  profileNameError,
  removeButton,
  removePage,
  renameProfile,
  updateButton,
} from './profile-editing';

const key = (id: string, row: number, column: number): ButtonDto => ({
  id,
  row,
  column,
  label: '',
  color: 'dark',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
});

const profile = (): ProfileDto => ({
  id: 'p1',
  name: 'Streaming',
  color: 'red',
  grid: { columns: 5, rows: 3 },
  pages: [
    { index: 0, buttons: [key('b1', 0, 0), key('b2', 2, 4)] },
    { index: 1, buttons: [key('b3', 1, 1)] },
  ],
});

const summaries: ProfileSummaryDto[] = [
  { id: 'p1', name: 'Streaming', color: 'red' },
  { id: 'p2', name: 'New profile', color: 'blue' },
];

const value = (result: EditResult): ProfileDto => {
  if (!result.ok) {
    throw new Error(result.error);
  }
  return result.profile;
};

describe('newProfile', () => {
  it('starts with one empty page and a trimmed name', () => {
    expect(newProfile('p9', '  Games ', 'purple', { columns: 3, rows: 3 })).toEqual({
      id: 'p9',
      name: 'Games',
      color: 'purple',
      grid: { columns: 3, rows: 3 },
      pages: [{ index: 0, buttons: [] }],
    });
  });
});

describe('nextProfileName and nextProfileColor', () => {
  it('pick the first unused name and color', () => {
    expect(nextProfileName([])).toBe('New profile');
    expect(nextProfileName([...summaries, { id: 'p3', name: 'new profile 2', color: 'teal' }])).toBe('New profile 3');
    expect(nextProfileColor(summaries)).toBe('orange');
  });
});

describe('profileNameError', () => {
  it('needs 1–40 characters once trimmed', () => {
    expect(profileNameError('   ', 'p1', summaries)).toBe('Give the profile a name.');
    expect(profileNameError('x'.repeat(41), 'p1', summaries)).toBe('Use at most 40 characters.');
    expect(profileNameError(' Editing ', 'p1', summaries)).toBeNull();
  });

  it('refuses another profile’s name, ignoring case, but not the profile’s own', () => {
    expect(profileNameError('NEW PROFILE', 'p1', summaries)).toBe('A profile named "NEW PROFILE" already exists.');
    expect(profileNameError('streaming', 'p1', summaries)).toBeNull();
  });

  it('trims on rename', () => {
    expect(renameProfile(profile(), '  Live ').name).toBe('Live');
  });
});

describe('changeGrid', () => {
  it('grows freely', () => {
    expect(value(changeGrid(profile(), { columns: 8, rows: 4 })).grid).toEqual({ columns: 8, rows: 4 });
  });

  it('refuses to leave keys outside', () => {
    expect(changeGrid(profile(), { columns: 3, rows: 3 })).toEqual({
      ok: false,
      error: 'One key is outside 3 × 3. Move or remove it first.',
    });
  });
});

describe('pages', () => {
  it('adds an empty page at the end, up to the maximum', () => {
    const added = value(addPage(profile()));
    expect(added.pages.map((page) => page.index)).toEqual([0, 1, 2]);
    expect(added.pages[2].buttons).toEqual([]);

    let full = profile();
    while (full.pages.length < MAX_PAGES) {
      full = value(addPage(full));
    }
    expect(addPage(full)).toEqual({ ok: false, error: 'A profile has at most 10 pages.' });
  });

  it('removes a page and renumbers the rest, keeping at least one', () => {
    const removed = value(removePage(value(addPage(profile())), 0));
    expect(removed.pages.map((page) => page.index)).toEqual([0, 1]);
    expect(removed.pages[0].buttons.map((button) => button.id)).toEqual(['b3']);

    const single = value(removePage(profile(), 1));
    expect(removePage(single, 0)).toEqual({ ok: false, error: 'A profile keeps at least one page.' });
  });
});

describe('keys', () => {
  it('finds keys by position and by id', () => {
    expect(buttonAt(profile(), 0, 2, 4)?.id).toBe('b2');
    expect(buttonAt(profile(), 1, 2, 4)).toBeNull();
    expect(findButton(profile(), 'b3')?.row).toBe(1);
  });

  it('places a new key on a free position', () => {
    const placed = value(placeButton(profile(), 1, 0, 3, 'b9'));
    expect(buttonAt(placed, 1, 0, 3)).toEqual(key('b9', 0, 3));
  });

  it('refuses taken, outside or missing positions', () => {
    expect(placeButton(profile(), 0, 0, 0, 'b9')).toEqual({ ok: false, error: 'That position already has a key.' });
    expect(placeButton(profile(), 0, 3, 0, 'b9')).toEqual({ ok: false, error: 'That position is outside the grid.' });
    expect(placeButton(profile(), 5, 0, 0, 'b9')).toEqual({ ok: false, error: 'Page 6 does not exist.' });
  });

  it('updates a key, checking the label length', () => {
    const updated = value(updateButton(profile(), 'b3', { label: 'Scenes', color: 'orange' }));
    expect(findButton(updated, 'b3')).toMatchObject({ label: 'Scenes', color: 'orange', row: 1, column: 1 });

    expect(updateButton(profile(), 'b3', { label: 'x'.repeat(13) })).toEqual({
      ok: false,
      error: 'Use at most 12 characters.',
    });
    expect(updateButton(profile(), 'nope', { label: 'A' })).toEqual({ ok: false, error: 'That key no longer exists.' });
  });

  it('removes a key', () => {
    expect(findButton(removeButton(profile(), 'b1'), 'b1')).toBeNull();
  });

  it('never changes the profile it was given', () => {
    const original = profile();
    const snapshot = structuredClone(original);
    value(updateButton(original, 'b1', { label: 'A' }));
    value(placeButton(original, 0, 1, 1, 'b9'));
    removeButton(original, 'b2');

    expect(original).toEqual(snapshot);
  });
});
