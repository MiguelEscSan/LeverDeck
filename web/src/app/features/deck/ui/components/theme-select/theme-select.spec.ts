import { TestBed } from '@angular/core/testing';
import { THEME_PREFERENCES, type ThemePreference } from '../../../../../core/theme/domain/theme';
import { ThemeSelect } from './theme-select';

describe('ThemeSelect', () => {
  it('shows the choices, selects the current one and emits a new choice', async () => {
    const fixture = TestBed.createComponent(ThemeSelect);
    fixture.componentRef.setInput('options', THEME_PREFERENCES);
    fixture.componentRef.setInput('value', 'dark');
    await fixture.whenStable();
    const changed: ThemePreference[] = [];
    fixture.componentInstance.changed.subscribe((preference) => changed.push(preference));
    const select = (fixture.nativeElement as HTMLElement).querySelector('select') as HTMLSelectElement;

    expect([...select.options].map((option) => option.text)).toEqual(['System', 'Dark', 'Light']);
    expect(select.value).toBe('dark');

    select.value = 'light';
    select.dispatchEvent(new Event('change'));

    expect(changed).toEqual(['light']);
  });
});
