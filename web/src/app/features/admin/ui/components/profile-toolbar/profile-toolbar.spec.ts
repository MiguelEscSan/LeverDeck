import { TestBed } from '@angular/core/testing';
import type { GridDto, ProfileDto } from '../../../../../core/protocol';
import { ProfileToolbar } from './profile-toolbar';

const profile: ProfileDto = {
  id: 'p1',
  name: 'Streaming',
  color: 'red',
  grid: { columns: 5, rows: 3 },
  pages: [{ index: 0, buttons: [] }],
};

async function render(canDelete = true) {
  const fixture = TestBed.createComponent(ProfileToolbar);
  fixture.componentRef.setInput('profile', profile);
  fixture.componentRef.setInput('canDelete', canDelete);
  await fixture.whenStable();
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

const buttonNamed = (element: HTMLElement, name: string) =>
  [...element.querySelectorAll('button')].find((button) => button.textContent?.trim() === name);

describe('ProfileToolbar', () => {
  it('emits a rename when the name field changes, and shows the saved name again', async () => {
    const { fixture, element } = await render();
    const names: string[] = [];
    fixture.componentInstance.rename.subscribe((name) => names.push(name));
    const field = element.querySelector('#profile-name') as HTMLInputElement;

    field.value = 'Live';
    field.dispatchEvent(new Event('change'));

    expect(names).toEqual(['Live']);
    expect(field.value).toBe('Streaming');
  });

  it('marks the current grid size and emits another', async () => {
    const { fixture, element } = await render();
    const grids: GridDto[] = [];
    fixture.componentInstance.gridChange.subscribe((grid) => grids.push(grid));
    const chips = [...element.querySelectorAll('.chip')];

    expect(chips.map((chip) => chip.getAttribute('aria-checked'))).toEqual(['false', 'true', 'false']);
    (chips[2] as HTMLButtonElement).click();

    expect(grids).toEqual([{ columns: 8, rows: 4 }]);
  });

  it('deletes only after confirming', async () => {
    const { fixture, element } = await render();
    let removed = 0;
    fixture.componentInstance.remove.subscribe(() => removed++);

    buttonNamed(element, 'Delete')?.click();
    await fixture.whenStable();
    expect(removed).toBe(0);
    buttonNamed(element, 'Delete profile?')?.click();

    expect(removed).toBe(1);
  });

  it('cannot delete the last profile', async () => {
    const { element } = await render(false);

    expect(buttonNamed(element, 'Delete')?.disabled).toBe(true);
  });
});
