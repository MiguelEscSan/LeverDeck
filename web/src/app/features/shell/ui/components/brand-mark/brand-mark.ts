import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The LeverDeck mark: a 2×2 grid of saturated keys. Decorative; the title next to it carries the name. */
@Component({
  selector: 'app-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `
    <i style="background: var(--ld-key-red)"></i>
    <i style="background: var(--ld-key-blue)"></i>
    <i style="background: var(--ld-key-orange)"></i>
    <i style="background: var(--ld-key-purple)"></i>
  `,
  styles: `
    :host {
      display: grid;
      grid-template-columns: repeat(2, 12px);
      gap: 3px;
    }

    i {
      width: 12px;
      height: 12px;
      border-radius: 3px;
      box-shadow: 0 0 0 1px var(--ld-key-border);
    }
  `,
})
export class BrandMark {}
