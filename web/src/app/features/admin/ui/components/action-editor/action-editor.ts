import { ChangeDetectionStrategy, Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import type {
  ActionDto,
  GameStoreDto,
  InstalledGameDto,
  KeyModifierDto,
  MediaKeyDto,
  PageTargetDto,
  ProfileSummaryDto,
  RegisteredAppDto,
  SoundDto,
  VirtualKeyDto,
} from '../../../../../core/protocol';
import {
  ACTION_TYPE_NAMES,
  type ActionContext,
  actionSummary,
  defaultAction,
  EDITABLE_ACTION_TYPES,
  type EditableActionType,
  GAME_STORE_NAMES,
  GAME_STORES,
  gameIdError,
  isEditableAction,
  MEDIA_KEY_NAMES,
  MEDIA_KEYS,
  MODIFIER_NAMES,
  MODIFIERS,
  PAGE_TARGET_NAMES,
  PAGE_TARGETS,
  parseUrl,
  toggleModifier,
  URL_MAX_LENGTH,
  VIRTUAL_KEY_NAMES,
  VIRTUAL_KEYS,
} from '../../../domain/action-catalog';

/**
 * A chosen action, with the name of the app or game it launches and of the one the key launched before: a label
 * suggestion for a key that is unlabeled or still carries the previous suggestion.
 */
export interface ActionChoice {
  readonly action: ActionDto;
  readonly name?: string;
  readonly previousName?: string;
}

/**
 * What a key does: the action type and its fields. Every action but OBS scenes is editable; an OBS scene stays as it
 * is and is shown read-only. A type that needs input first (a link, or an app or game when there is none to pick)
 * waits here as a draft and is emitted only once it is valid. Presentational.
 */
@Component({
  selector: 'app-action-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="ld-label" for="action-type">Action</label>
    <select id="action-type" class="ld-field" (change)="onType($event)">
      @if (!editable()) {
        <option selected disabled>{{ typeNames[action().type] }}</option>
      }
      @for (type of types; track type) {
        <option [value]="type" [selected]="type === shownType()">{{ typeNames[type] }}</option>
      }
    </select>

    @switch (shownType()) {
      @case ('hotkey') {
        @if (hotkey(); as hotkey) {
          <fieldset class="modifiers">
            <legend class="ld-label">Hold</legend>
            @for (modifier of modifiers; track modifier) {
              <label class="check">
                <input
                  type="checkbox"
                  [checked]="hotkey.modifiers.includes(modifier)"
                  (change)="onModifier(modifier, $event)"
                />
                {{ modifierNames[modifier] }}
              </label>
            }
          </fieldset>
          <label class="ld-label" for="action-key">Key</label>
          <select id="action-key" class="ld-field" (change)="onKey($event)">
            @for (key of keys; track key) {
              <option [value]="key" [selected]="key === hotkey.key">{{ keyNames[key] }}</option>
            }
          </select>
        }
      }
      @case ('mediaKey') {
        <label class="ld-label" for="action-media">Media key</label>
        <select id="action-media" class="ld-field" (change)="onMedia($event)">
          @for (key of mediaKeys; track key) {
            <option [value]="key" [selected]="key === mediaKey()">{{ mediaNames[key] }}</option>
          }
        </select>
      }
      @case ('changePage') {
        @if (pageChange(); as change) {
          <label class="ld-label" for="action-page-target">Go to</label>
          <select id="action-page-target" class="ld-field" (change)="onPageTarget($event)">
            @for (target of pageTargets; track target) {
              <option [value]="target" [selected]="target === change.target">{{ pageTargetNames[target] }}</option>
            }
          </select>
          @if (change.target === 'specific') {
            <label class="ld-label" for="action-page">Page</label>
            <select id="action-page" class="ld-field" (change)="onPage($event)">
              @for (page of pages(); track page) {
                <option [value]="page" [selected]="page === change.pageIndex">Page {{ page + 1 }}</option>
              }
            </select>
          }
        }
      }
      @case ('switchProfile') {
        <label class="ld-label" for="action-profile">Profile</label>
        <select id="action-profile" class="ld-field" (change)="onProfile($event)">
          @for (profile of profiles(); track profile.id) {
            <option [value]="profile.id" [selected]="profile.id === targetProfile()">{{ profile.name }}</option>
          }
        </select>
      }
      @case ('playSound') {
        <label class="ld-label" for="action-sound">Sound</label>
        <select id="action-sound" class="ld-field" (change)="onSound($event)">
          @for (sound of sounds(); track sound.id) {
            <option [value]="sound.id" [selected]="sound.id === playedSound()">{{ sound.name }}</option>
          }
        </select>
        @if (sounds().length === 0) {
          <p class="hint">Upload a sound under “Audio” first, then pick it here.</p>
        }
      }
      @case ('openUrl') {
        <label class="ld-label" for="action-url">Link</label>
        <input
          id="action-url"
          class="ld-field"
          type="url"
          inputmode="url"
          autocomplete="off"
          placeholder="https://example.com"
          [attr.maxlength]="urlMaxLength"
          [attr.aria-invalid]="urlError() !== null"
          aria-describedby="action-url-error"
          [value]="url()"
          (change)="onUrl($event)"
          (keydown.enter)="blur($event)"
        />
        <p id="action-url-error" class="ld-error field-error" role="alert">{{ urlError() ?? '' }}</p>
      }
      @case ('launchApp') {
        @if (apps().length > 0) {
          <label class="ld-label" for="action-app">App</label>
          <select id="action-app" class="ld-field" (change)="onApp($event)">
            @if (!currentApp()) {
              <option value="" selected disabled>Choose an app</option>
            }
            @for (app of apps(); track app.appId) {
              <option [value]="app.appId" [selected]="app.appId === currentApp()?.appId">{{ app.displayName }}</option>
            }
          </select>
        } @else {
          <p class="hint">Register an app under “Apps” first, then pick it here.</p>
        }
      }
      @case ('launchGame') {
        @if (games().length > 0) {
          <label class="ld-label" for="action-game">Game</label>
          <select id="action-game" class="ld-field" (change)="onGame($event)">
            @if (!currentGameKey() || !listedGame()) {
              <option value="" selected disabled>{{ currentGameKey() ? 'Another game (below)' : 'Choose a game' }}</option>
            }
            @for (game of games(); track gameKey(game)) {
              <option [value]="gameKey(game)" [selected]="gameKey(game) === currentGameKey()">
                {{ game.name }} · {{ storeNames[game.store] }}
              </option>
            }
          </select>
        } @else {
          <p class="hint">No Steam or Epic games were found on this PC. Enter the game's id instead.</p>
        }
        <details class="manual" [open]="games().length === 0 || (currentGameKey() !== null && !listedGame())">
          <summary>Enter a store id</summary>
          <label class="ld-label" for="action-store">Store</label>
          <select id="action-store" class="ld-field" (change)="onStore($event)">
            @for (store of stores; track store) {
              <option [value]="store" [selected]="store === manualStore()">{{ storeNames[store] }}</option>
            }
          </select>
          <label class="ld-label" for="action-game-id">
            {{ manualStore() === 'steam' ? 'Steam app id' : 'Epic app name' }}
          </label>
          <input
            id="action-game-id"
            class="ld-field"
            type="text"
            autocomplete="off"
            spellcheck="false"
            [attr.aria-invalid]="gameError() !== null"
            aria-describedby="action-game-error"
            [value]="manualId()"
            (change)="onGameId($event)"
            (keydown.enter)="blur($event)"
          />
          <p id="action-game-error" class="ld-error field-error" role="alert">{{ gameError() ?? '' }}</p>
        </details>
        <button type="button" class="link" [disabled]="gamesLoading()" (click)="refreshGames.emit()">
          {{ gamesLoading() ? 'Looking for games…' : 'Look for installed games again' }}
        </button>
      }
      @default {
        <p class="readonly">{{ summary() }}. This action can't be edited here yet.</p>
      }
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-2);
    }

    .ld-label {
      margin: var(--ld-space-2) 0 0;
      color: var(--ld-color-muted-on-panel);
    }

    .ld-field {
      min-height: 42px;
      font-size: var(--ld-font-size-md);
    }

    .modifiers {
      display: flex;
      flex-wrap: wrap;
      gap: var(--ld-space-3);
      margin: 0;
      padding: 0;
      border: 0;

      legend {
        width: 100%;
        margin-bottom: var(--ld-space-1);
      }
    }

    .check {
      display: flex;
      align-items: center;
      gap: var(--ld-space-1);
      font-size: var(--ld-font-size-md);

      input {
        width: 18px;
        height: 18px;
      }
    }

    .readonly,
    .hint {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }

    .field-error {
      min-height: 1.2em;
      font-size: var(--ld-font-size-sm);
    }

    .manual {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-2);
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);

      summary {
        cursor: pointer;
      }
    }

    .link {
      align-self: flex-start;
      padding: 0;
      border: 0;
      background: none;
      color: var(--ld-color-muted-on-panel);
      font: inherit;
      font-size: var(--ld-font-size-sm);
      text-decoration: underline;
      cursor: pointer;
    }
  `,
})
export class ActionEditor {
  readonly action = input.required<ActionDto>();
  /** The profile the key belongs to, for a default profile switch. */
  readonly profileId = input.required<string>();
  readonly profiles = input.required<readonly ProfileSummaryDto[]>();
  readonly pageCount = input.required<number>();
  readonly apps = input<readonly RegisteredAppDto[]>([]);
  readonly games = input<readonly InstalledGameDto[]>([]);
  readonly gamesLoading = input(false);
  /** The uploaded sound clips a key can play. */
  readonly sounds = input<readonly SoundDto[]>([]);

  readonly changed = output<ActionChoice>();
  readonly refreshGames = output();

  protected readonly types = EDITABLE_ACTION_TYPES;
  protected readonly typeNames = ACTION_TYPE_NAMES;
  protected readonly modifiers = MODIFIERS;
  protected readonly modifierNames = MODIFIER_NAMES;
  protected readonly keys = VIRTUAL_KEYS;
  protected readonly keyNames = VIRTUAL_KEY_NAMES;
  protected readonly mediaKeys = MEDIA_KEYS;
  protected readonly mediaNames = MEDIA_KEY_NAMES;
  protected readonly pageTargets = PAGE_TARGETS;
  protected readonly pageTargetNames = PAGE_TARGET_NAMES;
  protected readonly stores = GAME_STORES;
  protected readonly storeNames = GAME_STORE_NAMES;
  protected readonly urlMaxLength = URL_MAX_LENGTH;

  /** A type chosen but not valid yet (it needs input); a new action from outside drops it. */
  protected readonly draftType = linkedSignal<ActionDto, EditableActionType | null>({
    source: this.action,
    computation: () => null,
  });
  protected readonly shownType = computed(() => this.draftType() ?? this.action().type);

  private readonly context = computed<ActionContext>(() => ({
    profileId: this.profileId(),
    profiles: this.profiles(),
    apps: this.apps(),
    games: this.games(),
    sounds: this.sounds(),
  }));
  protected readonly editable = computed(() => isEditableAction(this.action()));
  protected readonly summary = computed(() => actionSummary(this.action(), this.context()));
  protected readonly pages = computed(() => Array.from({ length: this.pageCount() }, (_, index) => index));
  protected readonly hotkey = computed(() => {
    const action = this.action();
    return action.type === 'hotkey' ? action : null;
  });
  protected readonly mediaKey = computed(() => {
    const action = this.action();
    return action.type === 'mediaKey' ? action.key : null;
  });
  protected readonly pageChange = computed(() => {
    const action = this.action();
    return action.type === 'changePage' ? action : null;
  });
  protected readonly targetProfile = computed(() => {
    const action = this.action();
    return action.type === 'switchProfile' ? action.profileId : null;
  });
  protected readonly playedSound = computed(() => {
    const action = this.action();
    return action.type === 'playSound' ? action.soundId : (this.sounds()[0]?.id ?? null);
  });

  // Open URL: the stored link, or what was typed when it was refused.
  private readonly typedUrl = linkedSignal<ActionDto, string | null>({ source: this.action, computation: () => null });
  protected readonly urlError = signal<string | null>(null);
  protected readonly url = computed(() => {
    const action = this.action();
    return this.typedUrl() ?? (action.type === 'openUrl' && !this.draftType() ? action.url : '');
  });

  // Launch app.
  protected readonly currentApp = computed(() => {
    const action = this.action();
    return action.type === 'launchApp' && !this.draftType()
      ? (this.apps().find((app) => app.appId === action.appId) ?? null)
      : null;
  });

  // Launch game: the stored game, and the manual store and id fields.
  protected readonly currentGameKey = computed(() => {
    const action = this.action();
    return action.type === 'launchGame' && !this.draftType() ? `${action.store}:${action.gameId}` : null;
  });
  protected readonly listedGame = computed(() =>
    this.games().find((game) => this.gameKey(game) === this.currentGameKey()) ?? null,
  );
  protected readonly manualStore = linkedSignal<ActionDto, GameStoreDto>({
    source: this.action,
    computation: (action) => (action.type === 'launchGame' ? action.store : 'steam'),
  });
  protected readonly manualId = linkedSignal<ActionDto, string>({
    source: this.action,
    computation: (action) => (action.type === 'launchGame' ? action.gameId : ''),
  });
  protected readonly gameError = signal<string | null>(null);

  protected gameKey(game: InstalledGameDto): string {
    return `${game.store}:${game.gameId}`;
  }

  protected onType(event: Event): void {
    const type = (event.target as HTMLSelectElement).value as EditableActionType;
    this.urlError.set(null);
    this.gameError.set(null);
    if (type === this.action().type) {
      this.draftType.set(null);
      return;
    }
    const action = defaultAction(type, this.context());
    if (action) {
      this.emit(action);
    } else {
      this.draftType.set(type);
    }
  }

  protected onModifier(modifier: KeyModifierDto, event: Event): void {
    const hotkey = this.hotkey();
    if (hotkey) {
      const on = (event.target as HTMLInputElement).checked;
      this.emit({ ...hotkey, modifiers: toggleModifier(hotkey.modifiers, modifier, on) });
    }
  }

  protected onKey(event: Event): void {
    const hotkey = this.hotkey();
    if (hotkey) {
      this.emit({ ...hotkey, key: (event.target as HTMLSelectElement).value as VirtualKeyDto });
    }
  }

  protected onMedia(event: Event): void {
    this.emit({ type: 'mediaKey', key: (event.target as HTMLSelectElement).value as MediaKeyDto });
  }

  protected onPageTarget(event: Event): void {
    const target = (event.target as HTMLSelectElement).value as PageTargetDto;
    this.emit({ type: 'changePage', target, pageIndex: target === 'specific' ? 0 : null });
  }

  protected onPage(event: Event): void {
    this.emit({
      type: 'changePage',
      target: 'specific',
      pageIndex: Number((event.target as HTMLSelectElement).value),
    });
  }

  protected onProfile(event: Event): void {
    this.emit({ type: 'switchProfile', profileId: (event.target as HTMLSelectElement).value });
  }

  protected onSound(event: Event): void {
    this.emit({ type: 'playSound', soundId: (event.target as HTMLSelectElement).value });
  }

  protected onUrl(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    const parsed = parseUrl(text);
    if ('error' in parsed) {
      this.typedUrl.set(text);
      this.urlError.set(parsed.error);
      return;
    }
    this.urlError.set(null);
    this.emit({ type: 'openUrl', url: parsed.url });
  }

  protected onApp(event: Event): void {
    this.emit({ type: 'launchApp', appId: (event.target as HTMLSelectElement).value });
  }

  protected onGame(event: Event): void {
    const game = this.games().find((candidate) => this.gameKey(candidate) === (event.target as HTMLSelectElement).value);
    if (game) {
      this.gameError.set(null);
      this.emit({ type: 'launchGame', store: game.store, gameId: game.gameId });
    }
  }

  protected onStore(event: Event): void {
    this.manualStore.set((event.target as HTMLSelectElement).value as GameStoreDto);
    if (this.manualId()) {
      this.submitManualGame();
    }
  }

  protected onGameId(event: Event): void {
    this.manualId.set((event.target as HTMLInputElement).value.trim());
    this.submitManualGame();
  }

  protected blur(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  private submitManualGame(): void {
    const store = this.manualStore();
    const gameId = this.manualId();
    const error = gameIdError(store, gameId);
    this.gameError.set(error);
    if (!error) {
      this.emit({ type: 'launchGame', store, gameId });
    }
  }

  private emit(action: ActionDto): void {
    this.draftType.set(null);
    this.changed.emit({ action, name: this.nameOf(action), previousName: this.nameOf(this.action()) });
  }

  /** The app or game an action launches, or the site a link opens, as a label suggestion. */
  private nameOf(action: ActionDto): string | undefined {
    if (action.type === 'openUrl') {
      return URL.canParse(action.url) ? new URL(action.url).hostname.replace(/^www\./, '') : undefined;
    }
    if (action.type === 'launchApp') {
      return this.apps().find((app) => app.appId === action.appId)?.displayName;
    }
    if (action.type === 'launchGame') {
      return this.games().find((game) => game.store === action.store && game.gameId === action.gameId)?.name;
    }
    return undefined;
  }
}
