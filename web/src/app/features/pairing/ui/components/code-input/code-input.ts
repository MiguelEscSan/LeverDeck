import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { CODE_LENGTH, normalizeCode } from '../../../domain/pairing-form';

/**
 * The 6-digit pairing code as six boxes (docs/mocks/login.html). One real numeric input sits over the boxes, so the
 * phone shows its number pad, paste and SMS-style autofill work, and assistive technology reads a single field.
 * Presentational: `value` in, `valueChange` out.
 */
@Component({
  selector: 'app-code-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="ld-label" [for]="inputId()">Pairing code</label>
    <div class="code">
      <div class="boxes" aria-hidden="true">
        @for (slot of slots; track $index) {
          <span
            class="digit"
            [class.active]="focused() && $index === activeIndex()"
            [class.invalid]="invalid()"
            >{{ value()[$index] ?? '' }}</span
          >
        }
      </div>
      <input
        class="native"
        type="text"
        inputmode="numeric"
        pattern="[0-9]*"
        autocomplete="one-time-code"
        [attr.maxlength]="length"
        [id]="inputId()"
        [value]="value()"
        [attr.aria-invalid]="invalid()"
        [attr.aria-describedby]="describedBy()"
        (input)="onInput($event)"
        (focus)="focused.set(true)"
        (blur)="focused.set(false)"
      />
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .code {
      position: relative;
    }

    .boxes {
      display: grid;
      grid-template-columns: repeat(6, 1fr);
      gap: var(--ld-space-2);
    }

    .digit {
      display: grid;
      place-items: center;
      height: 58px;
      border: 2px solid var(--ld-color-field-border);
      border-radius: var(--ld-radius-md);
      background: var(--ld-color-field);
      font-size: 24px;
      font-weight: var(--ld-font-weight-bold);
    }

    .digit.active {
      border-color: var(--ld-key-blue);
    }

    .digit.active:empty::after {
      content: '';
      width: 2px;
      height: 26px;
      background: var(--ld-color-text);
      animation: blink 1s steps(1) infinite;
    }

    .digit.invalid {
      border-color: var(--ld-key-red);
    }

    .native {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      border: 0;
      background: transparent;
      color: transparent;
      caret-color: transparent;
      outline: none;
      letter-spacing: 1em;
    }

    .code:has(.native:focus-visible) .boxes {
      outline: 3px solid var(--ld-color-focus);
      outline-offset: 3px;
      border-radius: var(--ld-radius-md);
    }

    @keyframes blink {
      50% {
        opacity: 0;
      }
    }
  `,
})
export class CodeInput {
  readonly value = input.required<string>();
  readonly invalid = input(false);
  readonly inputId = input('pairing-code');
  readonly describedBy = input<string | null>(null);
  readonly valueChange = output<string>();

  protected readonly length = CODE_LENGTH;
  protected readonly slots = Array.from({ length: CODE_LENGTH });
  protected readonly focused = signal(false);
  protected readonly activeIndex = computed(() => Math.min(this.value().length, CODE_LENGTH - 1));

  protected onInput(event: Event): void {
    const field = event.target as HTMLInputElement;
    const code = normalizeCode(field.value);
    field.value = code;
    this.valueChange.emit(code);
  }
}
