import { TestBed } from '@angular/core/testing';
import { THEME_PREFERENCES, type ThemePreference } from '../../../../../core/theme/domain/theme';
import { ThemeSwitch } from './theme-switch';

describe('ThemeSwitch', () => {
  it('checks the current choice and emits the one picked', async () => {
    const fixture = TestBed.createComponent(ThemeSwitch);
    fixture.componentRef.setInput('options', THEME_PREFERENCES);
    fixture.componentRef.setInput('value', 'system');
    await fixture.whenStable();
    const changed: ThemePreference[] = [];
    fixture.componentInstance.changed.subscribe((preference) => changed.push(preference));
    const element = fixture.nativeElement as HTMLElement;
    const radios = [...element.querySelectorAll<HTMLInputElement>('input[type="radio"]')];

    expect(element.querySelector('legend')?.textContent).toBe('Theme');
    expect([...element.querySelectorAll('label')].map((label) => label.textContent?.trim())).toEqual([
      'System',
      'Dark',
      'Light',
    ]);
    expect(radios.map((radio) => radio.checked)).toEqual([true, false, false]);

    radios[1].click();

    expect(changed).toEqual(['dark']);
  });
});
