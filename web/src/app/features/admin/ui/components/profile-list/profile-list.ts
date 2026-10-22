import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { ProfileSummaryDto } from '../../../../../core/protocol';

/** The profiles sidebar (docs/mocks/desktop.html): one entry per profile and "+ New profile". Presentational. */
@Component({
  selector: 'app-profile-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 id="profiles-title" class="title">Profiles</h2>
    <ul aria-labelledby="profiles-title">
      @for (profile of profiles(); track profile.id) {
        <li>
          <button
            type="button"
            class="profile"
            [attr.aria-current]="profile.id === activeId() ? 'true' : null"
            (click)="selected.emit(profile.id)"
          >
            <span class="dot" aria-hidden="true" [style.background]="'var(--ld-key-' + profile.color + ')'"></span>
            <span class="name">{{ profile.name }}</span>
          </button>
        </li>
      }
    </ul>
    <button type="button" class="profile new" [disabled]="disabled()" (click)="create.emit()">+ New profile</button>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
    }

    .title {
      padding: 0 var(--ld-space-2) var(--ld-space-3);
      color: var(--ld-color-muted);
      font-size: var(--ld-font-size-xs);
      font-weight: var(--ld-font-weight-semibold);
      letter-spacing: 0.8px;
      text-transform: uppercase;
    }

    ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .profile {
      display: flex;
      align-items: center;
      gap: var(--ld-space-3);
      width: 100%;
      min-height: 42px;
      padding: var(--ld-space-2) var(--ld-space-3);
      border: 0;
      border-radius: var(--ld-radius-sm);
      background: none;
      color: var(--ld-color-text);
      font: inherit;
      font-size: var(--ld-font-size-md);
      text-align: left;
      cursor: pointer;

      &:hover {
        background: color-mix(in srgb, var(--ld-color-panel) 60%, transparent);
      }

      &[aria-current='true'] {
        background: var(--ld-color-panel);
        font-weight: var(--ld-font-weight-semibold);
      }
    }

    .dot {
      flex: none;
      width: 10px;
      height: 10px;
      border-radius: 3px;
      box-shadow: 0 0 0 1px var(--ld-key-border);
    }

    .name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .new {
      margin-top: var(--ld-space-2);
      color: var(--ld-color-muted);

      &:disabled {
        cursor: default;
        opacity: 0.55;
      }
    }
  `,
})
export class ProfileList {
  readonly profiles = input.required<readonly ProfileSummaryDto[]>();
  readonly activeId = input<string | null>(null);
  readonly disabled = input(false);

  readonly selected = output<string>();
  readonly create = output();
}
