import { TestBed } from '@angular/core/testing';
import { ProfilePicker } from './profile-picker';

describe('ProfilePicker', () => {
  it('lists the profiles, selects the active one and emits a new choice', async () => {
    const fixture = TestBed.createComponent(ProfilePicker);
    fixture.componentRef.setInput('profiles', [
      { id: 'p1', name: 'Default', color: 'teal' },
      { id: 'p2', name: 'Games', color: 'red' },
    ]);
    fixture.componentRef.setInput('activeId', 'p2');
    await fixture.whenStable();
    const selected: string[] = [];
    fixture.componentInstance.selected.subscribe((id) => selected.push(id));
    const select = (fixture.nativeElement as HTMLElement).querySelector('select') as HTMLSelectElement;

    expect([...select.options].map((option) => option.text)).toEqual(['Default', 'Games']);
    expect(select.value).toBe('p2');

    select.value = 'p1';
    select.dispatchEvent(new Event('change'));

    expect(selected).toEqual(['p1']);
  });
});
