import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import { RequestError } from '../../../core/connection/domain/request-error';
import type {
  ActionDto,
  ButtonDto,
  EventMessage,
  GridDto,
  KeyColorDto,
  ProfileDto,
  ProfileSummaryDto,
} from '../../../core/protocol';
import { suggestedIcon } from '../domain/action-catalog';
import {
  addPage,
  buttonAt,
  type ButtonChange,
  changeGrid,
  type EditResult,
  findButton,
  GRID_SIZES,
  newProfile,
  nextProfileColor,
  nextProfileName,
  placeButton,
  profileNameError,
  recolorProfile,
  removeButton,
  removePage,
  renameProfile,
  summaryOf,
  updateButton,
} from '../domain/profile-editing';
import { IdSource } from './id-source.port';

/** One grid position of the page being edited. */
export interface EditorCell {
  readonly row: number;
  readonly column: number;
  readonly button: ButtonDto | null;
}

/** What the inspector shows: a key, or an empty position that can get one. */
export type EditorSelection =
  | { readonly kind: 'key'; readonly button: ButtonDto }
  | { readonly kind: 'empty'; readonly row: number; readonly column: number };

const FALLBACK_GRID: GridDto = GRID_SIZES[1];

/**
 * The desktop editor's grid editing (Phase 7): profiles, pages and keys. The PC is the only place a layout changes;
 * devices only read it (the agent refuses `saveProfile` and `deleteProfile` outside the admin scope).
 *
 * Every edit applies at once on screen and is saved right away with `saveProfile` (the whole profile), in order. The
 * agent then pushes `profileUpdated` to every connection, so paired devices redraw by themselves. A refused save puts
 * back the last version the agent accepted and shows why.
 */
@Injectable({ providedIn: 'root' })
export class ProfileEditorFacade {
  private readonly connection = inject(AdminConnectionFacade);
  private readonly ids = inject(IdSource);
  /** Bumped by every profile open, so a slow answer for an older choice is dropped. */
  private openSequence = 0;
  /** Saves run one after another, in the order the edits were made. */
  private saveQueue: Promise<void> = Promise.resolve();

  private readonly profilesState = signal<ProfileSummaryDto[]>([]);
  private readonly profileState = signal<ProfileDto | null>(null);
  /** The last version of the open profile the agent accepted. */
  private readonly confirmedState = signal<ProfileDto | null>(null);
  private readonly pageState = signal(0);
  private readonly selectionState = signal<
    { readonly kind: 'key'; readonly id: string } | { readonly kind: 'empty'; readonly row: number; readonly column: number } | null
  >(null);
  private readonly pendingSaves = signal(0);
  private readonly loadingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly defaultGridState = signal<GridDto>(FALLBACK_GRID);

  readonly profiles = this.profilesState.asReadonly();
  readonly profile = this.profileState.asReadonly();
  /** The page being edited (its wire `index`; indexes are contiguous from 0). */
  readonly page = this.pageState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly saving = computed(() => this.pendingSaves() > 0);
  readonly pageCount = computed(() => this.profileState()?.pages.length ?? 0);
  readonly canDeleteProfile = computed(() => this.profilesState().length > 1);

  readonly cells = computed<EditorCell[]>(() => {
    const profile = this.profileState();
    if (!profile) {
      return [];
    }
    const cells: EditorCell[] = [];
    for (let row = 0; row < profile.grid.rows; row++) {
      for (let column = 0; column < profile.grid.columns; column++) {
        cells.push({ row, column, button: buttonAt(profile, this.pageState(), row, column) });
      }
    }
    return cells;
  });

  readonly selection = computed<EditorSelection | null>(() => {
    const selection = this.selectionState();
    const profile = this.profileState();
    if (!selection || !profile) {
      return null;
    }
    if (selection.kind === 'key') {
      const button = findButton(profile, selection.id);
      return button ? { kind: 'key', button } : null;
    }
    const fits = selection.row < profile.grid.rows && selection.column < profile.grid.columns;
    return fits && !buttonAt(profile, this.pageState(), selection.row, selection.column) ? selection : null;
  });

  constructor() {
    effect(() => {
      if (this.connection.connected()) {
        untracked(() => void this.load());
      }
    });
    this.connection.events.pipe(takeUntilDestroyed()).subscribe((event) => this.onEvent(event));
  }

  /** Loads the profiles and the default grid, and opens the current (or first) profile. */
  async load(): Promise<void> {
    this.loadingState.set(true);
    try {
      const [{ profiles }, { settings }] = await Promise.all([
        this.connection.query('getProfiles', {}),
        this.connection.query('getSettings', {}),
      ]);
      this.profilesState.set(profiles);
      this.defaultGridState.set(settings.defaultGrid);
      const current = this.profileState()?.id;
      const id = profiles.some((profile) => profile.id === current) ? current : profiles[0]?.id;
      if (id) {
        await this.open(id, id === current);
      } else {
        this.show(null, false);
      }
      this.errorState.set(null);
    } catch {
      this.errorState.set("Couldn't load the profiles. They load again when the connection is back.");
    } finally {
      this.loadingState.set(false);
    }
  }

  async selectProfile(profileId: string): Promise<void> {
    if (profileId === this.profileState()?.id) {
      return;
    }
    try {
      await this.open(profileId, false);
    } catch {
      this.errorState.set("Couldn't open that profile. Try again.");
    }
  }

  /** Creates a profile with a free name and color on the default grid, and opens it. */
  createProfile(): void {
    const profiles = this.profilesState();
    const profile = newProfile(this.ids.next(), nextProfileName(profiles), nextProfileColor(profiles), this.defaultGridState());
    this.profilesState.set([...profiles, summaryOf(profile)]);
    this.openSequence++;
    this.show(profile, false);
    this.confirmedState.set(null);
    this.save(profile);
  }

  async deleteProfile(): Promise<void> {
    const profile = this.profileState();
    if (!profile || !this.canDeleteProfile()) {
      return;
    }
    try {
      await this.connection.command('deleteProfile', { profileId: profile.id });
      // The `profileDeleted` event normally arrived first and opened another profile.
      this.forget(profile.id);
    } catch (error) {
      this.errorState.set(this.reason(error, "Couldn't delete the profile. Try again."));
    }
  }

  /** Why a name would be refused, for the name field, before anything is sent. */
  nameError(name: string): string | null {
    const profile = this.profileState();
    return profile ? profileNameError(name, profile.id, this.profilesState()) : null;
  }

  renameProfile(name: string): void {
    const profile = this.profileState();
    if (!profile || name.trim() === profile.name) {
      return;
    }
    const error = this.nameError(name);
    if (error) {
      this.errorState.set(error);
      return;
    }
    this.commit(renameProfile(profile, name));
  }

  recolorProfile(color: KeyColorDto): void {
    const profile = this.profileState();
    if (profile && profile.color !== color) {
      this.commit(recolorProfile(profile, color));
    }
  }

  changeGrid(grid: GridDto): void {
    const profile = this.profileState();
    if (profile) {
      this.apply(changeGrid(profile, grid));
    }
  }

  goToPage(pageIndex: number): void {
    if (Number.isInteger(pageIndex) && pageIndex >= 0 && pageIndex < this.pageCount()) {
      this.pageState.set(pageIndex);
      this.selectionState.set(null);
    }
  }

  addPage(): void {
    const profile = this.profileState();
    if (profile && this.apply(addPage(profile))) {
      this.goToPage(this.pageCount() - 1);
    }
  }

  removePage(): void {
    const profile = this.profileState();
    if (profile && this.apply(removePage(profile, this.pageState()))) {
      this.pageState.set(Math.min(this.pageState(), this.pageCount() - 1));
      this.selectionState.set(null);
    }
  }

  /** Selects the key at a position, or the empty position itself. */
  selectCell(row: number, column: number): void {
    const profile = this.profileState();
    if (!profile) {
      return;
    }
    const button = buttonAt(profile, this.pageState(), row, column);
    this.selectionState.set(button ? { kind: 'key', id: button.id } : { kind: 'empty', row, column });
  }

  clearSelection(): void {
    this.selectionState.set(null);
  }

  /** Puts a new key on the selected empty position and selects it. */
  addKey(): void {
    const profile = this.profileState();
    const selection = this.selection();
    if (!profile || selection?.kind !== 'empty') {
      return;
    }
    const id = this.ids.next();
    if (this.apply(placeButton(profile, this.pageState(), selection.row, selection.column, id))) {
      this.selectionState.set({ kind: 'key', id });
    }
  }

  /** Changes the selected key's label, color or icon. */
  updateKey(change: Omit<ButtonChange, 'action'>): void {
    const profile = this.profileState();
    const selection = this.selection();
    if (profile && selection?.kind === 'key') {
      this.apply(updateButton(profile, selection.button.id, change));
    }
  }

  /** Changes what the selected key does; an icon that still matched the old action follows the new one. */
  changeAction(action: ActionDto): void {
    const profile = this.profileState();
    const selection = this.selection();
    if (!profile || selection?.kind !== 'key') {
      return;
    }
    const { button } = selection;
    const iconFollows = button.iconId === suggestedIcon(button.action);
    this.apply(updateButton(profile, button.id, iconFollows ? { action, iconId: suggestedIcon(action) } : { action }));
  }

  removeKey(): void {
    const profile = this.profileState();
    const selection = this.selection();
    if (profile && selection?.kind === 'key') {
      this.commit(removeButton(profile, selection.button.id));
      this.selectionState.set({ kind: 'empty', row: selection.button.row, column: selection.button.column });
    }
  }

  dismissError(): void {
    this.errorState.set(null);
  }

  private apply(result: EditResult): boolean {
    if (!result.ok) {
      this.errorState.set(result.error);
      return false;
    }
    this.commit(result.profile);
    return true;
  }

  /** Shows an edit at once and saves it. */
  private commit(profile: ProfileDto): void {
    this.errorState.set(null);
    this.profileState.set(profile);
    this.profilesState.update((list) => list.map((item) => (item.id === profile.id ? summaryOf(profile) : item)));
    this.save(profile);
  }

  private save(profile: ProfileDto): void {
    this.pendingSaves.update((count) => count + 1);
    this.saveQueue = this.saveQueue.then(async () => {
      try {
        await this.connection.command('saveProfile', { profile });
        if (this.profileState()?.id === profile.id) {
          this.confirmedState.set(profile);
        }
      } catch (error) {
        this.errorState.set(this.reason(error, "Couldn't save the change. It was undone."));
        this.revert(profile.id);
      } finally {
        this.pendingSaves.update((count) => count - 1);
      }
    });
  }

  /** Puts back the last accepted version of a profile whose save failed; a never-saved profile goes away. */
  private revert(profileId: string): void {
    if (this.profileState()?.id !== profileId) {
      return;
    }
    const confirmed = this.confirmedState();
    if (confirmed) {
      this.profileState.set(confirmed);
      this.profilesState.update((list) => list.map((item) => (item.id === profileId ? summaryOf(confirmed) : item)));
      this.pageState.set(Math.min(this.pageState(), confirmed.pages.length - 1));
    } else {
      this.forget(profileId);
    }
  }

  private async open(profileId: string, keepPage: boolean): Promise<void> {
    const sequence = ++this.openSequence;
    const { profile } = await this.connection.query('getProfile', { profileId });
    if (sequence === this.openSequence) {
      this.show(profile, keepPage);
    }
  }

  private show(profile: ProfileDto | null, keepPage: boolean): void {
    const page = keepPage && profile ? Math.min(this.pageState(), profile.pages.length - 1) : 0;
    this.profileState.set(profile);
    this.confirmedState.set(profile);
    this.pageState.set(page);
    if (!keepPage) {
      this.selectionState.set(null);
    }
  }

  /** Drops a profile from the list; if it was open, opens the first remaining one. */
  private forget(profileId: string): void {
    this.profilesState.update((list) => list.filter((item) => item.id !== profileId));
    if (this.profileState()?.id !== profileId) {
      return;
    }
    const next = this.profilesState()[0]?.id;
    this.show(null, false);
    if (next) {
      void this.open(next, false).catch(() => this.errorState.set("Couldn't open the next profile."));
    }
  }

  private onEvent(event: EventMessage): void {
    switch (event.type) {
      case 'profileUpdated': {
        const profile = event.payload.profile;
        this.profilesState.update((list) =>
          list.some((item) => item.id === profile.id)
            ? list.map((item) => (item.id === profile.id ? summaryOf(profile) : item))
            : [...list, summaryOf(profile)],
        );
        // While saves are in flight the screen is ahead of these echoes; once idle, take the agent's version (it may
        // come from another editor tab).
        if (this.profileState()?.id === profile.id && !this.saving()) {
          this.openSequence++;
          this.show(profile, true);
        }
        break;
      }
      case 'profileDeleted':
        this.forget(event.payload.profileId);
        break;
      default:
        break;
    }
  }

  private reason(error: unknown, fallback: string): string {
    return error instanceof RequestError && error.code === 'validationFailed' ? error.message : fallback;
  }
}
