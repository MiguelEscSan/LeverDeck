import { TestBed } from '@angular/core/testing';
import { CodeInput } from './code-input';

describe('CodeInput', () => {
  const render = async (value: string, invalid = false) => {
    const fixture = TestBed.createComponent(CodeInput);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('invalid', invalid);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const emitted: string[] = [];
    fixture.componentInstance.valueChange.subscribe((code) => emitted.push(code));
    return { fixture, element, emitted, field: element.querySelector('input') as HTMLInputElement };
  };

  it('shows the digits in six boxes', async () => {
    const { element } = await render('472');

    const boxes = [...element.querySelectorAll('.digit')].map((box) => box.textContent?.trim());
    expect(boxes).toEqual(['4', '7', '2', '', '', '']);
  });

  it('is one labelled numeric field for the keyboard and assistive technology', async () => {
    const { element, field } = await render('');

    expect(element.querySelector('label')?.textContent).toBe('Pairing code');
    expect(element.querySelector('label')?.getAttribute('for')).toBe(field.id);
    expect(field.getAttribute('inputmode')).toBe('numeric');
    expect(field.getAttribute('autocomplete')).toBe('one-time-code');
  });

  it('emits only digits, at most six, also when pasted', async () => {
    const { field, emitted } = await render('');

    field.value = '47 29-15 99';
    field.dispatchEvent(new Event('input'));

    expect(emitted).toEqual(['472915']);
    expect(field.value).toBe('472915');
  });

  it('marks the field invalid', async () => {
    const { field, element } = await render('000000', true);

    expect(field.getAttribute('aria-invalid')).toBe('true');
    expect(element.querySelectorAll('.digit.invalid')).toHaveLength(6);
  });
});
