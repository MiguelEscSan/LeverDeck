import { computed, DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConnectionFacade } from '../../../core/connection/application/connection.facade';
import type { ButtonDto, EventMessage, ProfileDto, ProfileSummaryDto } from '../../../core/protocol';
import {
  chooseProfile,
  type DeckCell,
  type KeyFeedback,
  orderedPages,
  pageAfter,
  pageCells,
  summaryOf,
} from '../domain/deck';
import { DeckPreferences } from './deck-preferences.port';
import { Haptics } from './haptics.port';

/** How long a success or failure flash stays on a key. */
export const FLASH_MS = 650;

/**
 * The phone deck: the agent's profiles, the active profile and page, and key presses. Page and profile switches
 * happen on the device; every other key is sent as `executeAction`. Reloads whenever the connection is (re)made and
 * follows `profileUpdated`/`profileDeleted` pushed by the agent.
 */
@Injectable({ providedIn: 'root' })
export class DeckFacade {
  private readonly connection = inject(ConnectionFacade);
  private readonly preferences = inject(DeckPreferences);
  private readonly haptics = inject(Haptics);
  private readonly flashTimers = new Map<string, ReturnType<typeof setTimeout>>();
  /** Bumped by every profile open, so a slow answer for an older choice is dropped. */
  private openSequence = 0;

  private readonly profilesState = signal<ProfileSummaryDto[]>([]);
  private readonly profileState = signal<ProfileDto | null>(null);
  private readonly pageState = signal(0);
  private readonly loadingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly feedbackState = signal<Readonly<Record<string, KeyFeedback>>>({});

  readonly status = this.connection.status;
  readonly rejection = this.connection.rejection;
  readonly profiles = this.profilesState.asReadonly();
  readonly profile = this.profileState.asReadonly();
  /** Position of the current page in {@link pageCount} (not the wire `index`). */
  readonly page = this.pageState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly feedback = this.feedbackState.asReadonly();
  readonly pageCount = computed(() => {
    const profile = this.profileState();
    return profile ? profile.pages.length : 0;
  });
  readonly cells = computed<DeckCell[]>(() => {
    const profile = this.profileState();
    return profile ? pageCells(profile, this.pageState()) : [];
  });

  constructor() {
    effect(() => {
      if (this.connection.connected()) {
        untracked(() => void this.load());
      }
    });
    this.connection.events.pipe(takeUntilDestroyed()).subscribe((event) => this.onEvent(event));
    inject(DestroyRef).onDestroy(() => this.flashTimers.forEach((timer) => clearTimeout(timer)));
  }

  /** Connects as the paired device; `false` when this browser is not paired. */
  start(): boolean {
    return this.connection.start();
  }

  /** Loads the profile list and opens the remembered (or first) profile. */
  async load(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    try {
      const { profiles } = await this.connection.query('getProfiles', {});
      this.profilesState.set(profiles);
      const active = this.profileState()?.id ?? null;
      const id = chooseProfile(profiles, active ?? this.preferences.lastProfileId());
      if (id) {
        await this.open(id, id === active);
      } else {
        this.profileState.set(null);
      }
    } catch {
      this.errorState.set("Couldn't load the profiles from your PC. They load again when the connection is back.");
    } finally {
      this.loadingState.set(false);
    }
  }

  async selectProfile(profileId: string): Promise<void> {
    if (profileId === this.profileState()?.id) {
      return;
    }
    this.preferences.rememberProfile(profileId);
    try {
      await this.open(profileId, false);
    } catch {
      this.errorState.set("Couldn't open that profile. Try again.");
    }
  }

  goToPage(position: number): void {
    if (Number.isInteger(position) && position >= 0 && position < this.pageCount()) {
      this.pageState.set(position);
    }
  }

  async press(button: ButtonDto): Promise<void> {
    const profile = this.profileState();
    if (!profile) {
      return;
    }
    this.haptics.tap();
    const action = button.action;
    if (action.type === 'changePage') {
      this.pageState.set(pageAfter(action, this.pageState(), profile));
      return;
    }
    if (action.type === 'switchProfile') {
      await this.selectProfile(action.profileId);
      return;
    }

    const page = orderedPages(profile)[this.pageState()];
    this.setFeedback(button.id, 'pending');
    try {
      await this.connection.command('executeAction', {
        profileId: profile.id,
        pageIndex: page?.index ?? 0,
        buttonId: button.id,
      });
      this.flash(button.id, 'success');
    } catch {
      this.haptics.failure();
      this.flash(button.id, 'failure');
    }
  }

  private async open(profileId: string, keepPage: boolean): Promise<void> {
    const sequence = ++this.openSequence;
    const { profile } = await this.connection.query('getProfile', { profileId });
    if (sequence !== this.openSequence) {
      return;
    }
    this.show(profile, keepPage);
  }

  private show(profile: ProfileDto, keepPage: boolean): void {
    const position = keepPage ? Math.min(this.pageState(), Math.max(0, profile.pages.length - 1)) : 0;
    this.profileState.set(profile);
    this.pageState.set(position);
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
        if (this.profileState()?.id === profile.id) {
          this.openSequence++;
          this.show(profile, true);
        }
        break;
      }
      case 'profileDeleted': {
        const deleted = event.payload.profileId;
        this.profilesState.update((list) => list.filter((item) => item.id !== deleted));
        if (this.profileState()?.id === deleted) {
          this.profileState.set(null);
          const next = chooseProfile(this.profilesState(), null);
          if (next) {
            void this.open(next, false).catch(() => this.errorState.set("Couldn't open the next profile."));
          }
        }
        break;
      }
      default:
        break;
    }
  }

  private setFeedback(buttonId: string, state: KeyFeedback | null): void {
    this.feedbackState.update((current) => {
      const next = { ...current };
      if (state) {
        next[buttonId] = state;
      } else {
        delete next[buttonId];
      }
      return next;
    });
  }

  private flash(buttonId: string, state: KeyFeedback): void {
    clearTimeout(this.flashTimers.get(buttonId));
    this.setFeedback(buttonId, state);
    this.flashTimers.set(
      buttonId,
      setTimeout(() => {
        this.flashTimers.delete(buttonId);
        this.setFeedback(buttonId, null);
      }, FLASH_MS),
    );
  }
}
