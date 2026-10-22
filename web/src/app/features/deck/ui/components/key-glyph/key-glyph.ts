import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { keyIconPath, keyMonogram } from '../../../../../core/keys/domain/key-icons';

/** The icon of a key: a known glyph, or the first letters of the label (or icon id) as a placeholder. Decorative. */
@Component({
  selector: 'app-key-glyph',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `
    @if (path(); as d) {
      <svg viewBox="0 0 24 24"><path fill-rule="evenodd" [attr.d]="d" /></svg>
    } @else {
      <span class="monogram">{{ monogram() }}</span>
    }
  `,
  styles: `
    :host {
      display: grid;
      place-items: center;
      width: 100%;
      height: 100%;
    }

    svg {
      width: 100%;
      height: 100%;
      fill: var(--ld-key-icon);
      filter: drop-shadow(0 2px 0 rgb(0 0 0 / 55%));
    }

    .monogram {
      font-size: 1.6em;
      font-weight: var(--ld-font-weight-heavy);
      line-height: 1;
      text-shadow: 0 2px 0 rgb(0 0 0 / 55%);
    }
  `,
})
export class KeyGlyph {
  readonly iconId = input.required<string>();
  readonly label = input('');

  protected readonly path = computed(() => keyIconPath(this.iconId()));
  protected readonly monogram = computed(() => keyMonogram(this.label(), this.iconId()));
}
