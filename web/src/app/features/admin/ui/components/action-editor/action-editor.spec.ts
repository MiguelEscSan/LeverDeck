import { TestBed } from '@angular/core/testing';
import type { ActionDto, InstalledGameDto, RegisteredAppDto } from '../../../../../core/protocol';
import { type ActionChoice, ActionEditor } from './action-editor';

const obs: RegisteredAppDto = { appId: 'a1', displayName: 'OBS Studio', path: 'C:\\OBS\\obs64.exe' };
const dota: InstalledGameDto = { store: 'steam', gameId: '570', name: 'Dota 2' };
const rocket: InstalledGameDto = { store: 'epic', gameId: 'Sugar', name: 'Rocket League' };

async function render(
  action: ActionDto,
  library: { apps?: RegisteredAppDto[]; games?: InstalledGameDto[] } = {},
) {
  const fixture = TestBed.createComponent(ActionEditor);
  fixture.componentRef.setInput('action', action);
  fixture.componentRef.setInput('profileId', 'p1');
  fixture.componentRef.setInput('profiles', [
    { id: 'p1', name: 'Streaming', color: 'red' },
    { id: 'p2', name: 'Games', color: 'purple' },
  ]);
  fixture.componentRef.setInput('pageCount', 3);
  fixture.componentRef.setInput('apps', library.apps ?? []);
  fixture.componentRef.setInput('games', library.games ?? []);
  await fixture.whenStable();
  const choices: ActionChoice[] = [];
  fixture.componentInstance.changed.subscribe((choice) => choices.push(choice));
  const element = fixture.nativeElement as HTMLElement;
  const choose = async (selector: string, value: string) => {
    const select = element.querySelector(selector) as HTMLSelectElement;
    select.value = value;
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  };
  const type = async (selector: string, value: string) => {
    const field = element.querySelector(selector) as HTMLInputElement;
    field.value = value;
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  };
  return { element, choices, actions: () => choices.map((choice) => choice.action), choose, type };
}

describe('ActionEditor', () => {
  it('switches the action type to that type’s default', async () => {
    const { actions, choose } = await render({ type: 'mediaKey', key: 'mute' });

    await choose('#action-type', 'switchProfile');

    expect(actions()).toEqual([{ type: 'switchProfile', profileId: 'p2' }]);
  });

  it('edits a hotkey’s modifiers and key', async () => {
    const { element, actions, choose } = await render({ type: 'hotkey', modifiers: ['ctrl'], key: 'c' });
    const shift = [...element.querySelectorAll('.check')].find((check) => check.textContent?.includes('Shift'));
    const box = shift?.querySelector('input') as HTMLInputElement;

    box.checked = true;
    box.dispatchEvent(new Event('change'));
    await choose('#action-key', 'f13');

    expect(actions()).toEqual([
      { type: 'hotkey', modifiers: ['ctrl', 'shift'], key: 'c' },
      { type: 'hotkey', modifiers: ['ctrl'], key: 'f13' },
    ]);
  });

  it('picks a specific page', async () => {
    const { element, actions, choose } = await render({ type: 'changePage', target: 'specific', pageIndex: 0 });

    expect(element.querySelectorAll('#action-page option')).toHaveLength(3);
    await choose('#action-page', '2');

    expect(actions()).toEqual([{ type: 'changePage', target: 'specific', pageIndex: 2 }]);
  });

  it('shows an OBS scene read-only', async () => {
    const { element } = await render({ type: 'obsScene', sceneName: 'Live' });

    expect(element.querySelector('.readonly')?.textContent).toContain('OBS scene Live');
    expect((element.querySelector('#action-type') as HTMLSelectElement).selectedOptions[0].textContent).toBe(
      'OBS scene',
    );
  });

  describe('open URL', () => {
    it('waits for a valid link before emitting, and names the key after the site', async () => {
      const { element, choices, actions, choose, type } = await render({ type: 'mediaKey', key: 'mute' });

      await choose('#action-type', 'openUrl');
      expect(actions()).toEqual([]);
      expect(element.querySelector('#action-url')).not.toBeNull();

      await type('#action-url', 'ftp://files');
      expect(element.querySelector('#action-url-error')?.textContent).toBe('Only http and https links can be opened.');
      expect(actions()).toEqual([]);

      await type('#action-url', 'www.youtube.com');
      expect(choices).toEqual([{ action: { type: 'openUrl', url: 'https://www.youtube.com/' }, name: 'youtube.com' }]);
    });

    it('shows and edits the stored link', async () => {
      const { element, choices, type } = await render({ type: 'openUrl', url: 'https://example.com/' });

      expect((element.querySelector('#action-url') as HTMLInputElement).value).toBe('https://example.com/');
      await type('#action-url', 'https://twitch.tv/me');

      expect(choices).toEqual([
        { action: { type: 'openUrl', url: 'https://twitch.tv/me' }, name: 'twitch.tv', previousName: 'example.com' },
      ]);
    });
  });

  describe('launch app', () => {
    it('defaults to the first registered app and names it', async () => {
      const { choices, choose } = await render({ type: 'mediaKey', key: 'mute' }, { apps: [obs] });

      await choose('#action-type', 'launchApp');

      expect(choices).toEqual([{ action: { type: 'launchApp', appId: 'a1' }, name: 'OBS Studio', previousName: undefined }]);
    });

    it('asks to register an app when there is none', async () => {
      const { element, actions, choose } = await render({ type: 'mediaKey', key: 'mute' });

      await choose('#action-type', 'launchApp');

      expect(actions()).toEqual([]);
      expect(element.querySelector('.hint')?.textContent).toContain('Register an app under “Apps” first');
    });
  });

  describe('launch game', () => {
    it('picks an installed game and names it', async () => {
      const { choices, choose } = await render(
        { type: 'launchGame', store: 'steam', gameId: '570' },
        { games: [dota, rocket] },
      );

      await choose('#action-game', 'epic:Sugar');

      expect(choices).toEqual([
        { action: { type: 'launchGame', store: 'epic', gameId: 'Sugar' }, name: 'Rocket League', previousName: 'Dota 2' },
      ]);
    });

    it('takes a store id typed by hand, checking it', async () => {
      const { element, actions, choose, type } = await render({ type: 'mediaKey', key: 'mute' });

      await choose('#action-type', 'launchGame');
      expect(element.querySelector('.hint')?.textContent).toContain('No Steam or Epic games were found');
      expect(actions()).toEqual([]);

      await type('#action-game-id', 'abc');
      expect(element.querySelector('#action-game-error')?.textContent).toContain('1–10 digits');
      expect(actions()).toEqual([]);

      await type('#action-game-id', '730');
      expect(actions()).toEqual([{ type: 'launchGame', store: 'steam', gameId: '730' }]);
    });

    it('asks for the games again', async () => {
      const fixture = TestBed.createComponent(ActionEditor);
      fixture.componentRef.setInput('action', { type: 'launchGame', store: 'steam', gameId: '570' });
      fixture.componentRef.setInput('profileId', 'p1');
      fixture.componentRef.setInput('profiles', []);
      fixture.componentRef.setInput('pageCount', 1);
      await fixture.whenStable();
      let refreshes = 0;
      fixture.componentInstance.refreshGames.subscribe(() => refreshes++);

      ((fixture.nativeElement as HTMLElement).querySelector('.link') as HTMLButtonElement).click();

      expect(refreshes).toBe(1);
    });
  });
});
