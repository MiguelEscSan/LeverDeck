import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { keyIconPath, keyMonogram } from '../../../../../core/keys/domain/key-icons';
import type { ButtonDto } from '../../../../../core/protocol';

/**
 * How a key looks on the phone (docs/VISUAL-REFERENCE.md, Keys), for the editor grid and the icon picker: its color,
 * its icon (or monogram) and its label. Decorative: the control around it carries the name. Presentational.
 */
@Component({
  selector: 'app-key-face',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', '[attr.data-color]': 'button().color' },
  template: `
    <span class="face">
      @if (path(); as d) {
        <svg class="glyph" viewBox="0 0 24 24"><path fill-rule="evenodd" [attr.d]="d" /></svg>
      } @else {
        <span class="glyph monogram">{{ monogram() }}</span>
      }
      @if (button().label) {
        <span class="label">{{ button().label }}</span>
      }
    </span>
  `,
  styles: `
    // The host is the size container; the face inside sizes its border and text from it (cqi).
    :host {
      display: block;
      width: 100%;
      container-type: inline-size;
    }

    .face {
      position: relative;
      display: grid;
      place-items: center;
      width: 100%;
      aspect-ratio: 1;
      border: max(2px, 3cqi) solid var(--ld-key-border);
      border-radius: var(--ld-radius-key);
      background: var(--key-fill, var(--ld-key-dark));
      box-shadow: var(--ld-shadow-key);
      color: var(--key-ink, #ffffff);
      font-size: max(9px, 11cqi);
    }

    :host([data-color='red']) {
      --key-fill: var(--ld-key-red);
    }

    :host([data-color='blue']) {
      --key-fill: var(--ld-key-blue);
    }

    :host([data-color='orange']) {
      --key-fill: var(--ld-key-orange);
      --key-ink: #1e1e1e;
    }

    :host([data-color='purple']) {
      --key-fill: var(--ld-key-purple);
    }

    :host([data-color='teal']) {
      --key-fill: var(--ld-key-teal);
    }

    :host([data-color='dark']) .face {
      border-color: var(--ld-color-line);
    }

    .glyph {
      width: 52%;
      height: 52%;
      margin-bottom: 8%;
    }

    svg {
      fill: var(--ld-key-icon);
      filter: drop-shadow(0 2px 0 rgb(0 0 0 / 55%));
    }

    .monogram {
      display: grid;
      place-items: center;
      font-size: 1.8em;
      font-weight: var(--ld-font-weight-heavy);
      line-height: 1;
    }

    .label {
      position: absolute;
      bottom: 7%;
      left: 6%;
      right: 6%;
      overflow: hidden;
      font-weight: var(--ld-font-weight-heavy);
      letter-spacing: 0.04em;
      line-height: 1.1;
      text-align: center;
      text-overflow: ellipsis;
      text-transform: uppercase;
      white-space: nowrap;
    }
  `,
})
export class KeyFace {
  readonly button = input.required<Pick<ButtonDto, 'label' | 'color' | 'iconId'>>();

  protected readonly path = computed(() => keyIconPath(this.button().iconId));
  protected readonly monogram = computed(() => keyMonogram(this.button().label, this.button().iconId));
}
