import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type {
  ActionDto,
  KeyModifierDto,
  MediaKeyDto,
  PageTargetDto,
  ProfileSummaryDto,
  VirtualKeyDto,
} from '../../../../../core/protocol';
import {
  ACTION_TYPE_NAMES,
  actionSummary,
  defaultAction,
  EDITABLE_ACTION_TYPES,
  type EditableActionType,
  isEditableAction,
  MEDIA_KEY_NAMES,
  MEDIA_KEYS,
  MODIFIER_NAMES,
  MODIFIERS,
  PAGE_TARGET_NAMES,
  PAGE_TARGETS,
  toggleModifier,
  VIRTUAL_KEY_NAMES,
  VIRTUAL_KEYS,
} from '../../../domain/action-catalog';

/**
 * What a key does: the action type and its fields. Only hotkeys, media keys, page changes and profile switches are
 * editable for now; any other action stays as it is and is shown read-only. Presentational.
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
        <option [value]="type" [selected]="type === action().type">{{ typeNames[type] }}</option>
      }
    </select>

    @switch (action().type) {
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

    .readonly {
      color: var(--ld-color-muted-on-panel);
      font-size: var(--ld-font-size-sm);
    }
  `,
})
export class ActionEditor {
  readonly action = input.required<ActionDto>();
  /** The profile the key belongs to, for a default profile switch. */
  readonly profileId = input.required<string>();
  readonly profiles = input.required<readonly ProfileSummaryDto[]>();
  readonly pageCount = input.required<number>();

  readonly changed = output<ActionDto>();

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

  protected readonly editable = computed(() => isEditableAction(this.action()));
  protected readonly summary = computed(() => actionSummary(this.action(), this.profiles()));
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

  protected onType(event: Event): void {
    const type = (event.target as HTMLSelectElement).value as EditableActionType;
    if (type !== this.action().type) {
      this.changed.emit(defaultAction(type, this.profileId(), this.profiles()));
    }
  }

  protected onModifier(modifier: KeyModifierDto, event: Event): void {
    const hotkey = this.hotkey();
    if (hotkey) {
      const on = (event.target as HTMLInputElement).checked;
      this.changed.emit({ ...hotkey, modifiers: toggleModifier(hotkey.modifiers, modifier, on) });
    }
  }

  protected onKey(event: Event): void {
    const hotkey = this.hotkey();
    if (hotkey) {
      this.changed.emit({ ...hotkey, key: (event.target as HTMLSelectElement).value as VirtualKeyDto });
    }
  }

  protected onMedia(event: Event): void {
    this.changed.emit({ type: 'mediaKey', key: (event.target as HTMLSelectElement).value as MediaKeyDto });
  }

  protected onPageTarget(event: Event): void {
    const target = (event.target as HTMLSelectElement).value as PageTargetDto;
    this.changed.emit({ type: 'changePage', target, pageIndex: target === 'specific' ? 0 : null });
  }

  protected onPage(event: Event): void {
    this.changed.emit({
      type: 'changePage',
      target: 'specific',
      pageIndex: Number((event.target as HTMLSelectElement).value),
    });
  }

  protected onProfile(event: Event): void {
    this.changed.emit({ type: 'switchProfile', profileId: (event.target as HTMLSelectElement).value });
  }
}
