import { TestBed } from '@angular/core/testing';
import type { RegisteredAppDto, StoreAppDto } from '../../../../../core/protocol';
import { AddAppDialog, type AppRegistration } from './add-app-dialog';

const calculator: StoreAppDto = { name: 'Calculator', appUserModelId: 'Microsoft.WindowsCalculator_8wekyb3d8bbwe!App' };
const paint: StoreAppDto = { name: 'Paint', appUserModelId: 'Microsoft.Paint_8wekyb3d8bbwe!App' };
const registeredCalculator: RegisteredAppDto = {
  appId: 'a1',
  displayName: 'Calculator',
  path: 'shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App',
};

async function render(apps: RegisteredAppDto[] = [], storeApps: StoreAppDto[] = [calculator, paint]) {
  const fixture = TestBed.createComponent(AddAppDialog);
  fixture.componentRef.setInput('open', true);
  fixture.componentRef.setInput('apps', apps);
  fixture.componentRef.setInput('storeApps', storeApps);
  await fixture.whenStable();
  const registered: AppRegistration[] = [];
  const added: StoreAppDto[] = [];
  let dismissed = 0;
  fixture.componentInstance.register.subscribe((registration) => registered.push(registration));
  fixture.componentInstance.registerStore.subscribe((app) => added.push(app));
  fixture.componentInstance.dismiss.subscribe(() => dismissed++);
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
  const openTab = async (label: string) => {
    button(label)?.click();
    await fixture.whenStable();
  };
  return { fixture, element, registered, added, dismissed: () => dismissed, fill, submit, button, openTab };
}

describe('AddAppDialog', () => {
  it('opens as a dialog on the Microsoft Store tab', async () => {
    const { element, button } = await render();

    expect(element.querySelector('dialog')?.hasAttribute('open')).toBe(true);
    expect(button('Microsoft Store')?.getAttribute('aria-selected')).toBe('true');
    expect(button('Program (.exe)')?.getAttribute('aria-selected')).toBe('false');
    expect([...element.querySelectorAll('.store-name')].map((name) => name.textContent)).toEqual(['Calculator', 'Paint']);
  });

  it('marks the Store apps already registered and adds the others', async () => {
    const { element, added, button } = await render([registeredCalculator]);

    expect(element.querySelector('.added')?.textContent?.trim()).toBe('Added');
    expect(button('Add')?.getAttribute('aria-label')).toBe('Add Paint');
    button('Add')?.click();

    expect(added).toEqual([paint]);
  });

  it('searches the Store apps', async () => {
    const { element, fill } = await render();

    await fill('input[type="search"]', 'pai');

    expect([...element.querySelectorAll('.store-name')].map((name) => name.textContent)).toEqual(['Paint']);
  });

  it('switches to the program form and keeps that tab', async () => {
    const { fixture, element, button, openTab } = await render();

    await openTab('Program (.exe)');
    expect(button('Program (.exe)')?.getAttribute('aria-selected')).toBe('true');
    expect(element.querySelector('#app-path')).not.toBeNull();
    fixture.componentRef.setInput('open', false);
    await fixture.whenStable();
    fixture.componentRef.setInput('open', true);
    await fixture.whenStable();

    expect(element.querySelector('#app-path')).not.toBeNull();
  });

  it('names the app after the pasted path until a name is typed', async () => {
    const { element, registered, fill, submit, openTab } = await render();
    await openTab('Program (.exe)');

    await fill('#app-path', '"C:\\Program Files\\OBS\\obs64.exe"');
    expect((element.querySelector('#app-name') as HTMLInputElement).value).toBe('obs64');
    await fill('#app-name', 'OBS');
    await fill('#app-path', 'C:\\Program Files\\OBS\\obs32.exe');
    await submit();

    expect(registered).toEqual([{ displayName: 'OBS', path: 'C:\\Program Files\\OBS\\obs32.exe' }]);
  });

  it('refuses a path that is not a full Windows path', async () => {
    const { element, registered, fill, submit, openTab } = await render();
    await openTab('Program (.exe)');

    await fill('#app-path', 'notepad.exe');
    await submit();

    expect(element.querySelector('.form-error')?.textContent).toBe('Use the full path, starting with a drive like C:\\.');
    expect(registered).toEqual([]);
  });

  it('asks to close from Close, Done and Cancel', async () => {
    const { element, button, dismissed, openTab } = await render();

    button('Done')?.click();
    (element.querySelector('button[aria-label="Close"]') as HTMLButtonElement).click();
    await openTab('Program (.exe)');
    button('Cancel')?.click();

    expect(dismissed()).toBe(3);
  });

  it('closes the dialog when it is no longer open', async () => {
    const { fixture, element } = await render();

    fixture.componentRef.setInput('open', false);
    await fixture.whenStable();

    expect(element.querySelector('dialog')?.hasAttribute('open')).toBe(false);
  });
});
