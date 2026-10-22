import type {
  ButtonDto,
  ChangePageActionDto,
  PageDto,
  ProfileDto,
  ProfileSummaryDto,
} from '../../../core/protocol';

/** One grid position on the current page; `button` is `null` for an empty slot. */
export interface DeckCell {
  readonly row: number;
  readonly column: number;
  readonly button: ButtonDto | null;
}

/** The visual answer to a press: waiting for the agent, then a short success or failure flash. */
export type KeyFeedback = 'pending' | 'success' | 'failure';

/** The profile's pages in display order. Positions in this list are what the page dots show. */
export const orderedPages = (profile: ProfileDto): PageDto[] =>
  [...profile.pages].sort((a, b) => a.index - b.index);

/** Every cell of the grid, row by row, with the page's buttons in their positions. */
export function pageCells(profile: ProfileDto, position: number): DeckCell[] {
  const page = orderedPages(profile)[position];
  const byPosition = new Map((page?.buttons ?? []).map((button) => [`${button.row}:${button.column}`, button]));
  const cells: DeckCell[] = [];
  for (let row = 0; row < profile.grid.rows; row++) {
    for (let column = 0; column < profile.grid.columns; column++) {
      cells.push({ row, column, button: byPosition.get(`${row}:${column}`) ?? null });
    }
  }
  return cells;
}

/** The page position after a `changePage` key: next and previous wrap around; an unknown specific page stays put. */
export function pageAfter(action: ChangePageActionDto, position: number, profile: ProfileDto): number {
  const pages = orderedPages(profile);
  const count = pages.length;
  if (count === 0) {
    return 0;
  }
  switch (action.target) {
    case 'next':
      return (position + 1) % count;
    case 'previous':
      return (position - 1 + count) % count;
    case 'specific': {
      const target = pages.findIndex((page) => page.index === action.pageIndex);
      return target >= 0 ? target : position;
    }
  }
}

/** The profile to open: the remembered one when it still exists, otherwise the first. */
export function chooseProfile(profiles: readonly ProfileSummaryDto[], remembered: string | null): string | null {
  if (remembered && profiles.some((profile) => profile.id === remembered)) {
    return remembered;
  }
  return profiles[0]?.id ?? null;
}

export const summaryOf = ({ id, name, color }: ProfileDto): ProfileSummaryDto => ({ id, name, color });
