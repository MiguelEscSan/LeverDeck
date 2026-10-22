import type { ButtonDto, GridDto, KeyColorDto, PageDto, ProfileDto, ProfileSummaryDto } from '../../../core/protocol';

/**
 * Pure edits of a profile for the desktop editor. Each returns a new `ProfileDto` (or an error message) and keeps the
 * agent's invariants (LeverDeck.Domain/Profiles), so the agent should never refuse what the editor sends; the agent
 * still validates everything.
 */

export const PROFILE_NAME_MAX_LENGTH = 40;
export const LABEL_MAX_LENGTH = 12;
export const MAX_PAGES = 10;

/** The only grid sizes the agent accepts. */
export const GRID_SIZES: readonly GridDto[] = [
  { columns: 3, rows: 3 },
  { columns: 5, rows: 3 },
  { columns: 8, rows: 4 },
];

/** The closed key palette, in picker order. */
export const KEY_COLOR_NAMES: Readonly<Record<KeyColorDto, string>> = {
  red: 'Red',
  blue: 'Blue',
  orange: 'Orange',
  purple: 'Purple',
  teal: 'Teal',
  dark: 'Dark',
};

export const KEY_COLORS = Object.keys(KEY_COLOR_NAMES) as KeyColorDto[];

export type EditResult = { readonly ok: true; readonly profile: ProfileDto } | { readonly ok: false; readonly error: string };

const ok = (profile: ProfileDto): EditResult => ({ ok: true, profile });
const fail = (error: string): EditResult => ({ ok: false, error });

export const sameGrid = (a: GridDto, b: GridDto): boolean => a.columns === b.columns && a.rows === b.rows;

export const gridLabel = ({ columns, rows }: GridDto): string => `${columns} × ${rows}`;

/** A new profile with one empty page. */
export function newProfile(id: string, name: string, color: KeyColorDto, grid: GridDto): ProfileDto {
  return { id, name: name.trim(), color, grid: { ...grid }, pages: [{ index: 0, buttons: [] }] };
}

/** "New profile", then "New profile 2", 3… — the first name no other profile uses (ignoring case). */
export function nextProfileName(profiles: readonly ProfileSummaryDto[]): string {
  const taken = new Set(profiles.map((profile) => profile.name.trim().toLowerCase()));
  for (let n = 1; ; n++) {
    const name = n === 1 ? 'New profile' : `New profile ${n}`;
    if (!taken.has(name.toLowerCase())) {
      return name;
    }
  }
}

/** The color for a new profile: the first one no profile uses yet, else red. */
export function nextProfileColor(profiles: readonly ProfileSummaryDto[]): KeyColorDto {
  return KEY_COLORS.find((color) => !profiles.some((profile) => profile.color === color)) ?? 'red';
}

/** Why a profile name would be refused, or `null`: 1–40 characters once trimmed and unique ignoring case. */
export function profileNameError(
  name: string,
  profileId: string,
  profiles: readonly ProfileSummaryDto[],
): string | null {
  const trimmed = name.trim();
  if (trimmed.length === 0) {
    return 'Give the profile a name.';
  }
  if (trimmed.length > PROFILE_NAME_MAX_LENGTH) {
    return `Use at most ${PROFILE_NAME_MAX_LENGTH} characters.`;
  }
  const lower = trimmed.toLowerCase();
  if (profiles.some((profile) => profile.id !== profileId && profile.name.trim().toLowerCase() === lower)) {
    return `A profile named "${trimmed}" already exists.`;
  }
  return null;
}

export function renameProfile(profile: ProfileDto, name: string): ProfileDto {
  return { ...profile, name: name.trim() };
}

export function recolorProfile(profile: ProfileDto, color: KeyColorDto): ProfileDto {
  return { ...profile, color };
}

const allButtons = (profile: ProfileDto): ButtonDto[] => profile.pages.flatMap((page) => page.buttons);

const fits = (button: ButtonDto, grid: GridDto): boolean => button.row < grid.rows && button.column < grid.columns;

/** Changes the grid size; refused while a key would fall outside the new grid (the agent's `GridTooSmall`). */
export function changeGrid(profile: ProfileDto, grid: GridDto): EditResult {
  const outside = allButtons(profile).filter((button) => !fits(button, grid)).length;
  if (outside > 0) {
    return fail(
      `${outside === 1 ? 'One key is' : `${outside} keys are`} outside ${gridLabel(grid)}. Move or remove ${
        outside === 1 ? 'it' : 'them'
      } first.`,
    );
  }
  return ok({ ...profile, grid: { ...grid } });
}

const sortedPages = (profile: ProfileDto): PageDto[] => [...profile.pages].sort((a, b) => a.index - b.index);

/** Adds an empty page at the end. */
export function addPage(profile: ProfileDto): EditResult {
  if (profile.pages.length >= MAX_PAGES) {
    return fail(`A profile has at most ${MAX_PAGES} pages.`);
  }
  return ok({ ...profile, pages: [...sortedPages(profile), { index: profile.pages.length, buttons: [] }] });
}

/** Removes a page and renumbers the ones after it; the last page stays. */
export function removePage(profile: ProfileDto, pageIndex: number): EditResult {
  if (profile.pages.length <= 1) {
    return fail('A profile keeps at least one page.');
  }
  const pages = sortedPages(profile)
    .filter((page) => page.index !== pageIndex)
    .map((page, index) => ({ ...page, index }));
  return ok({ ...profile, pages });
}

export function buttonAt(profile: ProfileDto, pageIndex: number, row: number, column: number): ButtonDto | null {
  const page = profile.pages.find((candidate) => candidate.index === pageIndex);
  return page?.buttons.find((button) => button.row === row && button.column === column) ?? null;
}

export function findButton(profile: ProfileDto, buttonId: string): ButtonDto | null {
  return allButtons(profile).find((button) => button.id === buttonId) ?? null;
}

const mapPage = (profile: ProfileDto, pageIndex: number, change: (buttons: ButtonDto[]) => ButtonDto[]): ProfileDto => ({
  ...profile,
  pages: profile.pages.map((page) => (page.index === pageIndex ? { ...page, buttons: change(page.buttons) } : page)),
});

/** A new key on a free position: dark, unlabeled, playing or pausing media until the owner sets it up. */
export function placeButton(profile: ProfileDto, pageIndex: number, row: number, column: number, id: string): EditResult {
  if (!profile.pages.some((page) => page.index === pageIndex)) {
    return fail(`Page ${pageIndex + 1} does not exist.`);
  }
  if (row < 0 || column < 0 || row >= profile.grid.rows || column >= profile.grid.columns) {
    return fail('That position is outside the grid.');
  }
  if (buttonAt(profile, pageIndex, row, column)) {
    return fail('That position already has a key.');
  }
  const button: ButtonDto = {
    id,
    row,
    column,
    label: '',
    color: 'dark',
    iconId: 'play-pause',
    action: { type: 'mediaKey', key: 'playPause' },
  };
  return ok(mapPage(profile, pageIndex, (buttons) => [...buttons, button]));
}

/** What the key inspector can change on a key. */
export type ButtonChange = Partial<Pick<ButtonDto, 'label' | 'color' | 'iconId' | 'action'>>;

export function labelError(label: string): string | null {
  return label.length > LABEL_MAX_LENGTH ? `Use at most ${LABEL_MAX_LENGTH} characters.` : null;
}

export function updateButton(profile: ProfileDto, buttonId: string, change: ButtonChange): EditResult {
  if (!findButton(profile, buttonId)) {
    return fail('That key no longer exists.');
  }
  const error = change.label === undefined ? null : labelError(change.label);
  if (error) {
    return fail(error);
  }
  return ok({
    ...profile,
    pages: profile.pages.map((page) => ({
      ...page,
      buttons: page.buttons.map((button) => (button.id === buttonId ? { ...button, ...change } : button)),
    })),
  });
}

const pageOf = (profile: ProfileDto, buttonId: string): PageDto | null =>
  profile.pages.find((page) => page.buttons.some((button) => button.id === buttonId)) ?? null;

/**
 * Moves a key to another position of its page. A key already there trades places with it, so dragging onto a taken
 * position never loses a key (the agent only needs one key per position).
 */
export function moveButton(profile: ProfileDto, buttonId: string, row: number, column: number): EditResult {
  const page = pageOf(profile, buttonId);
  const button = findButton(profile, buttonId);
  if (!page || !button) {
    return fail('That key no longer exists.');
  }
  if (row < 0 || column < 0 || row >= profile.grid.rows || column >= profile.grid.columns) {
    return fail('That position is outside the grid.');
  }
  if (button.row === row && button.column === column) {
    return ok(profile);
  }
  const occupant = buttonAt(profile, page.index, row, column);
  return ok(
    mapPage(profile, page.index, (buttons) =>
      buttons.map((candidate) => {
        if (candidate.id === buttonId) {
          return { ...candidate, row, column };
        }
        if (candidate.id === occupant?.id) {
          return { ...candidate, row: button.row, column: button.column };
        }
        return candidate;
      }),
    ),
  );
}

/** Moves a key to the first free position (row by row) of another page; refused when that page is full. */
export function moveButtonToPage(profile: ProfileDto, buttonId: string, pageIndex: number): EditResult {
  const from = pageOf(profile, buttonId);
  const button = findButton(profile, buttonId);
  if (!from || !button) {
    return fail('That key no longer exists.');
  }
  if (from.index === pageIndex) {
    return ok(profile);
  }
  if (!profile.pages.some((page) => page.index === pageIndex)) {
    return fail(`Page ${pageIndex + 1} does not exist.`);
  }
  for (let row = 0; row < profile.grid.rows; row++) {
    for (let column = 0; column < profile.grid.columns; column++) {
      if (!buttonAt(profile, pageIndex, row, column)) {
        const moved = { ...button, row, column };
        const without = mapPage(profile, from.index, (buttons) => buttons.filter((candidate) => candidate.id !== buttonId));
        return ok(mapPage(without, pageIndex, (buttons) => [...buttons, moved]));
      }
    }
  }
  return fail(`Page ${pageIndex + 1} is full.`);
}

export function removeButton(profile: ProfileDto, buttonId: string): ProfileDto {
  return {
    ...profile,
    pages: profile.pages.map((page) => ({ ...page, buttons: page.buttons.filter((button) => button.id !== buttonId) })),
  };
}

export const summaryOf = ({ id, name, color }: ProfileDto): ProfileSummaryDto => ({ id, name, color });
