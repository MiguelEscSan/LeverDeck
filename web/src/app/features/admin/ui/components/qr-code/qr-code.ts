import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { qrModules } from '../../../domain/qr';

/** Quiet zone around the code, in modules; scanners need some, four is the spec but two reads fine on a white card. */
const QUIET_ZONE = 2;

/** A QR code of `text` as a crisp SVG on white, so phone cameras read it in either theme. Presentational. */
@Component({
  selector: 'app-qr-code',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [attr.viewBox]="viewBox()"
      shape-rendering="crispEdges"
      role="img"
      [attr.aria-label]="label()"
    >
      <path [attr.d]="qr().path" />
    </svg>
  `,
  styles: `
    :host {
      display: block;
      padding: var(--ld-space-2);
      border-radius: var(--ld-radius-md);
      background: #ffffff;
    }

    svg {
      display: block;
      width: 100%;
      height: 100%;
      fill: #000000;
    }
  `,
})
export class QrCode {
  readonly text = input.required<string>();
  readonly label = input('QR code');

  protected readonly qr = computed(() => qrModules(this.text()));
  protected readonly viewBox = computed(() => {
    const side = this.qr().size + 2 * QUIET_ZONE;
    return `${-QUIET_ZONE} ${-QUIET_ZONE} ${side} ${side}`;
  });
}
