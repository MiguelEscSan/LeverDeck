import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import type { GridDto, KeyColorDto, ProfileDto } from '../../../../../core/protocol';
import { GRID_SIZES, gridLabel, PROFILE_NAME_MAX_LENGTH, sameGrid } from '../../../domain/profile-editing';
import { ColorSwatches } from '../color-swatches/color-swatches';

/**
 * The open profile's settings above the grid: its name (saved when the field is left or Enter is pressed), color,
 * grid size, and a two-step delete. Presentational.
 */
@Component({
  selector: 'app-profile-toolbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ColorSwatches],
  template: `
    <div class="name-row">
      <label class="visually-hidden" for="profile-name">Profile name</label>
      <input
        id="profile-name"
        class="name"
        type="text"
        autocomplete="off"
        [attr.maxlength]="nameMaxLength"
        [value]="profile().name"
        (change)="onName($event)"
        (keydown.enter)="blur($event)"
      />
      @if (confirmingDelete()) {
        <button class="ld-btn ld-btn--danger" type="button" (click)="confirmDelete()">Delete profile?</button>
        <button class="ld-btn" type="button" (click)="confirmingDelete.set(false)">Keep</button>
      } @else {
        <button
          class="ld-btn"
          type="button"
          [disabled]="!canDelete()"
          [attr.title]="canDelete() ? null : 'The last profile cannot be deleted'"
          (click)="confirmingDelete.set(true)"
        >
          Delete
        </button>
      }
    </div>
    <p class="sub">Choose a key to edit it, drag it to move it. Changes sync to paired devices instantly.</p>
    <div class="settings">
      <app-color-swatches label="Profile color" [value]="profile().color" (chosen)="recolor.emit($event)" />
      <div class="sizes" role="radiogroup" aria-label="Grid size">
        @for (size of sizes; track $index) {
          <button
            type="button"
            role="radio"
            class="chip"
            [attr.aria-checked]="isCurrent(size)"
            (click)="gridChange.emit(size)"
          >
            {{ sizeLabel(size) }}
          </button>
        }
      </div>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--ld-space-2);
    }

    .name-row {
      display: flex;
      align-items: center;
      gap: var(--ld-space-2);
    }

    .name {
      flex: 1;
      min-width: 0;
      padding: var(--ld-space-1) var(--ld-space-2);
      margin-left: calc(-1 * var(--ld-space-2));
      border: 2px solid transparent;
      border-radius: var(--ld-radius-sm);
      background: none;
      color: var(--ld-color-text);
      font-size: var(--ld-font-size-title);
      font-weight: var(--ld-font-weight-bold);

      &:hover {
        border-color: var(--ld-color-line);
      }

      &:focus-visible {
        outline: none;
        border-color: var(--ld-key-blue);
      }
    }

    .sub {
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-sm);
    }

    .settings {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--ld-space-4);
      margin-top: var(--ld-space-2);
    }

    .sizes {
      display: flex;
      gap: var(--ld-space-2);
    }

    .chip {
      padding: var(--ld-space-2) var(--ld-space-3);
      border: 1px solid var(--ld-color-line);
      border-radius: var(--ld-radius-sm);
      background: var(--ld-color-panel);
      color: var(--ld-color-muted-on-panel);
      font: inherit;
      font-size: var(--ld-font-size-sm);
      cursor: pointer;

      &[aria-checked='true'] {
        border-color: var(--ld-color-muted);
        color: var(--ld-color-text);
        font-weight: var(--ld-font-weight-semibold);
      }
    }

    .ld-btn {
      min-height: 40px;
      font-size: var(--ld-font-size-sm);
    }

    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
})
export class ProfileToolbar {
  readonly profile = input.required<ProfileDto>();
  readonly canDelete = input(true);

  readonly rename = output<string>();
  readonly recolor = output<KeyColorDto>();
  readonly gridChange = output<GridDto>();
  readonly remove = output();

  protected readonly sizes = GRID_SIZES;
  protected readonly nameMaxLength = PROFILE_NAME_MAX_LENGTH;
  protected readonly confirmingDelete = signal(false);

  protected isCurrent(size: GridDto): boolean {
    return sameGrid(size, this.profile().grid);
  }

  protected sizeLabel(size: GridDto): string {
    return gridLabel(size);
  }

  protected onName(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.rename.emit(field.value);
    // The field shows the profile's name again; a refused rename must not stay on screen as if it were saved.
    field.value = this.profile().name;
  }

  protected blur(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  protected confirmDelete(): void {
    this.confirmingDelete.set(false);
    this.remove.emit();
  }
}
