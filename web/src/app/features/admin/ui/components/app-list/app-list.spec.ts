import { TestBed } from '@angular/core/testing';
import type { RegisteredAppDto } from '../../../../../core/protocol';
import { AppList } from './app-list';

const notepad: RegisteredAppDto = { appId: 'a1', displayName: 'Notepad', path: 'C:\\Windows\\notepad.exe' };
const calculator: RegisteredAppDto = {
  appId: 'a2',
  displayName: 'Calculator',
  path: 'shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App',
};
const many = (count: number): RegisteredAppDto[] =>
  Array.from({ length: count }, (_, index) => ({
    appId: `p${index + 1}`,
    displayName: `App ${index + 1}`,
    path: `C:\\Apps\\app${index + 1}.exe`,
  }));

async function render(apps: RegisteredAppDto[]) {
  const fixture = TestBed.createComponent(AppList);
  fixture.componentRef.setInput('apps', apps);
  await fixture.whenStable();
  const removed: string[] = [];
  fixture.componentInstance.remove.subscribe((id) => removed.push(id));
  const element = fixture.nativeElement as HTMLElement;
  const names = () => [...element.querySelectorAll('.app .name')].map((name) => name.textContent);
  const button = (label: string) =>
    [...element.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === label);
  const search = async (value: string) => {
    const field = element.querySelector('input[aria-label="Search apps"]') as HTMLInputElement;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  };
  return { fixture, element, removed, names, button, search };
}

describe('AppList', () => {
  it('lists the registered apps with their paths and kinds', async () => {
    const { element } = await render([notepad, calculator]);

    expect([...element.querySelectorAll('.app .path')].map((path) => path.textContent)).toEqual([
      'C:\\Windows\\notepad.exe',
      'Microsoft.WindowsCalculator_8wekyb3d8bbwe!App',
    ]);
    expect([...element.querySelectorAll('.app .kind')].map((kind) => kind.textContent?.trim())).toEqual([
      'Program',
      'Microsoft Store',
    ]);
  });

  it('pages the apps', async () => {
    const { fixture, element, names, button } = await render(many(8));

    expect(names()).toEqual(['App 1', 'App 2', 'App 3', 'App 4', 'App 5', 'App 6']);
    expect(element.querySelector('.range')?.textContent?.trim()).toBe('Showing 1–6 of 8');
    expect(button('1')?.getAttribute('aria-current')).toBe('page');
    button('2')?.click();
    await fixture.whenStable();

    expect(names()).toEqual(['App 7', 'App 8']);
    expect(element.querySelector('.range')?.textContent?.trim()).toBe('Showing 7–8 of 8');
    expect(element.querySelector<HTMLButtonElement>('button[aria-label="Next page"]')?.disabled).toBe(true);
  });

  it('searches by name or path and starts again on the first page', async () => {
    const { fixture, element, names, button, search } = await render([...many(7), notepad]);
    button('2')?.click();
    await fixture.whenStable();

    await search('windows\\note');
    expect(names()).toEqual(['Notepad']);
    expect(element.querySelector('.count')?.textContent?.trim()).toBe('1 of 8 apps');
    await search('zzz');

    expect(element.querySelector('.none')?.textContent).toBe('No apps match “zzz”.');
  });

  it('removes an app only after confirming', async () => {
    const { fixture, removed, button } = await render([notepad]);

    button('Remove')?.click();
    await fixture.whenStable();
    expect(removed).toEqual([]);
    button('Remove?')?.click();

    expect(removed).toEqual(['a1']);
  });

  it('invites to add the first app', async () => {
    const { element } = await render([]);

    expect(element.querySelector('.empty h2')?.textContent).toBe('No apps yet');
    expect(element.querySelector('.app')).toBeNull();
  });

  it('opens the Add app dialog and closes it on reset', async () => {
    const { fixture, element, button } = await render([notepad]);

    button('Add app')?.click();
    await fixture.whenStable();
    expect(element.querySelector('dialog')?.hasAttribute('open')).toBe(true);
    fixture.componentInstance.reset();
    await fixture.whenStable();

    expect(element.querySelector('dialog')?.hasAttribute('open')).toBe(false);
  });
});
