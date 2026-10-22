import type { ButtonDto, ProfileDto } from '../../../core/protocol';
import { chooseProfile, orderedPages, pageAfter, pageCells } from './deck';

const button = (id: string, row: number, column: number): ButtonDto => ({
  id,
  row,
  column,
  label: id,
  color: 'dark',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
});

const profile: ProfileDto = {
  id: 'p1',
  name: 'Default',
  color: 'teal',
  grid: { columns: 3, rows: 2 },
  pages: [
    { index: 1, buttons: [button('b2', 0, 0)] },
    { index: 0, buttons: [button('b1', 1, 2), button('b0', 0, 1)] },
  ],
};

describe('orderedPages', () => {
  it('orders pages by index', () => {
    expect(orderedPages(profile).map((page) => page.index)).toEqual([0, 1]);
  });
});

describe('pageCells', () => {
  it('lays out every grid cell row by row, with the buttons in their positions', () => {
    const cells = pageCells(profile, 0);

    expect(cells).toHaveLength(6);
    expect(cells.map((cell) => cell.button?.id ?? null)).toEqual([null, 'b0', null, null, null, 'b1']);
    expect(cells[5]).toMatchObject({ row: 1, column: 2 });
  });

  it('is empty for a page that does not exist', () => {
    expect(pageCells(profile, 5).every((cell) => cell.button === null)).toBe(true);
  });
});

describe('pageAfter', () => {
  it('goes to the next page and wraps around', () => {
    expect(pageAfter({ type: 'changePage', target: 'next', pageIndex: null }, 0, profile)).toBe(1);
    expect(pageAfter({ type: 'changePage', target: 'next', pageIndex: null }, 1, profile)).toBe(0);
  });

  it('goes to the previous page and wraps around', () => {
    expect(pageAfter({ type: 'changePage', target: 'previous', pageIndex: null }, 0, profile)).toBe(1);
  });

  it('goes to a specific page by its index', () => {
    expect(pageAfter({ type: 'changePage', target: 'specific', pageIndex: 1 }, 0, profile)).toBe(1);
  });

  it('stays put for a specific page that does not exist', () => {
    expect(pageAfter({ type: 'changePage', target: 'specific', pageIndex: 7 }, 0, profile)).toBe(0);
  });
});

describe('chooseProfile', () => {
  const summaries = [
    { id: 'p1', name: 'Default', color: 'teal' as const },
    { id: 'p2', name: 'Games', color: 'red' as const },
  ];

  it('prefers the remembered profile', () => {
    expect(chooseProfile(summaries, 'p2')).toBe('p2');
  });

  it('falls back to the first profile', () => {
    expect(chooseProfile(summaries, 'gone')).toBe('p1');
    expect(chooseProfile(summaries, null)).toBe('p1');
  });

  it('has nothing to choose from an empty list', () => {
    expect(chooseProfile([], 'p1')).toBeNull();
  });
});
