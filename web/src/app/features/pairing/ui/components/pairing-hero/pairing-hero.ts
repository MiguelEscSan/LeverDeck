import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The big LeverDeck mark of the pairing screen: four saturated keys in a dark tile. Decorative. */
@Component({
  selector: 'app-pairing-hero',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `
    <div class="logo">
      <span class="key" style="background: var(--ld-key-red)">
        <svg viewBox="0 0 24 24" fill="#fff"><path d="M8 5v14l11-7z" /></svg>
      </span>
      <span class="key" style="background: var(--ld-key-blue)">
        <svg viewBox="0 0 24 24" fill="#fff">
          <rect x="9" y="3" width="6" height="11" rx="3" />
          <path d="M6 11a6 6 0 0 0 12 0" stroke="#fff" stroke-width="2" fill="none" />
          <rect x="11" y="17" width="2" height="4" />
        </svg>
      </span>
      <span class="key" style="background: var(--ld-key-orange)">
        <svg viewBox="0 0 24 24" fill="#fff">
          <path d="M4 6h16v10H4z" fill="none" stroke="#fff" stroke-width="2.4" />
          <rect x="8" y="18" width="8" height="2.4" />
        </svg>
      </span>
      <span class="key" style="background: var(--ld-key-purple)">
        <svg viewBox="0 0 24 24" fill="#fff"><path d="M13 2 4 14h6l-1 8 9-12h-6z" /></svg>
      </span>
    </div>
  `,
  styles: `
    :host {
      display: grid;
      place-items: center;
    }

    .logo {
      display: grid;
      grid-template-columns: repeat(2, 56px);
      gap: 9px;
      padding: 12px;
      border: 3px solid var(--ld-key-border);
      border-radius: 26px;
      background: var(--ld-key-dark);
    }

    .key {
      display: grid;
      place-items: center;
      width: 56px;
      height: 56px;
      border: 3px solid var(--ld-key-border);
      border-radius: var(--ld-radius-md);
      box-shadow: var(--ld-shadow-key);
    }

    svg {
      width: 32px;
      height: 32px;
      filter: drop-shadow(0 2px 0 rgb(0 0 0 / 55%));
    }
  `,
})
export class PairingHero {}
