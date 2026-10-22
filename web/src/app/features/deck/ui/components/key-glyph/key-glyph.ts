import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * SVG paths (24×24, filled white) for the icon ids the agent seeds. Real icon packs arrive with the editor (Phase 7);
 * until then any other id shows its monogram.
 */
const GLYPHS: Readonly<Record<string, string>> = {
  'play-pause': 'M4 5v14l9-7zM15 5h2.5v14H15zM19.5 5H22v14h-2.5z',
  'skip-next': 'M5 5v14l10-7zM16 5h3v14h-3z',
  'skip-previous': 'M19 5v14L9 12zM5 5h3v14H5z',
  'volume-up':
    'M3 9v6h4l5 5V4L7 9zM14.5 7.5a6 6 0 0 1 0 9l-1.4-1.4a4 4 0 0 0 0-6.2zM17.3 4.7a10 10 0 0 1 0 14.6l-1.4-1.4a8 8 0 0 0 0-11.8z',
  'volume-down': 'M5 9v6h4l5 5V4L9 9zM16.5 7.5a6 6 0 0 1 0 9l-1.4-1.4a4 4 0 0 0 0-6.2z',
  'volume-mute': 'M3 9v6h4l5 5V4L7 9zM15 9.4l1.4-1.4 2.1 2.1 2.1-2.1 1.4 1.4-2.1 2.1 2.1 2.1-1.4 1.4-2.1-2.1-2.1 2.1-1.4-1.4 2.1-2.1z',
};

/** The icon of a key: a known glyph, or the first letters of the label (or icon id) as a placeholder. Decorative. */
@Component({
  selector: 'app-key-glyph',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `
    @if (path(); as d) {
      <svg viewBox="0 0 24 24"><path [attr.d]="d" /></svg>
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

  protected readonly path = computed(() => GLYPHS[this.iconId()] ?? null);
  protected readonly monogram = computed(() => {
    const source = (this.label().trim() || this.iconId()).replace(/[^\p{L}\p{N}]/gu, '');
    return source.slice(0, 2).toUpperCase() || '•';
  });
}
