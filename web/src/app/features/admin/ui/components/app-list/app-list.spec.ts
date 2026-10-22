import { TestBed } from '@angular/core/testing';
import type { RegisteredAppDto } from '../../../../../core/protocol';
import { AppList, type AppRegistration } from './app-list';

const notepad: RegisteredAppDto = { appId: 'a1', displayName: 'Notepad', path: 'C:\\Windows\\notepad.exe' };

async function render(apps: RegisteredAppDto[]) {
  const fixture = TestBed.createComponent(AppList);
  fixture.componentRef.setInput('apps', apps);
  await fixture.whenStable();
  const registered: AppRegistration[] = [];
  const removed: string[] = [];
  fixture.componentInstance.register.subscribe((registration) => registered.push(registration));
  fixture.componentInstance.remove.subscribe((id) => removed.push(id));
  const element = fixture.nativeElement as HTMLElement;
  const fill = async (selector: string, value: string) => {
    const field = element.querySelector(selector) as HTMLInputElement;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  };
  const submit = async () => {
    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  };
  const button = (label: string) =>
    [...element.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === label);
  return { fixture, element, registered, removed, fill, submit, button };
}

describe('AppList', () => {
  it('lists the registered apps with their paths', async () => {
    const { element } = await render([notepad]);

    expect(element.querySelector('.name')?.textContent).toBe('Notepad');
    expect(element.querySelector('.path')?.textContent).toBe('C:\\Windows\\notepad.exe');
  });

  it('names the app after the pasted path until a name is typed', async () => {
    const { element, registered, fill, submit } = await render([]);

    await fill('#app-path', '"C:\\Program Files\\OBS\\obs64.exe"');
    expect((element.querySelector('#app-name') as HTMLInputElement).value).toBe('obs64');
    await fill('#app-name', 'OBS');
    await fill('#app-path', 'C:\\Program Files\\OBS\\obs32.exe');
    await submit();

    expect(registered).toEqual([{ displayName: 'OBS', path: 'C:\\Program Files\\OBS\\obs32.exe' }]);
  });

  it('refuses a path that is not a full Windows path', async () => {
    const { element, registered, fill, submit } = await render([]);

    await fill('#app-path', 'notepad.exe');
    await submit();

    expect(element.querySelector('.form-error')?.textContent).toBe('Use the full path, starting with a drive like C:\\.');
    expect(registered).toEqual([]);
  });

  it('removes an app only after confirming', async () => {
    const { fixture, removed, button } = await render([notepad]);

    button('Remove')?.click();
    await fixture.whenStable();
    expect(removed).toEqual([]);
    button('Remove?')?.click();

    expect(removed).toEqual(['a1']);
  });
});
