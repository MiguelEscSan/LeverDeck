import { TestBed } from '@angular/core/testing';
import type { ActionDto } from '../../../../../core/protocol';
import { ActionEditor } from './action-editor';

async function render(action: ActionDto) {
  const fixture = TestBed.createComponent(ActionEditor);
  fixture.componentRef.setInput('action', action);
  fixture.componentRef.setInput('profileId', 'p1');
  fixture.componentRef.setInput('profiles', [
    { id: 'p1', name: 'Streaming', color: 'red' },
    { id: 'p2', name: 'Games', color: 'purple' },
  ]);
  fixture.componentRef.setInput('pageCount', 3);
  await fixture.whenStable();
  const changes: ActionDto[] = [];
  fixture.componentInstance.changed.subscribe((change) => changes.push(change));
  const element = fixture.nativeElement as HTMLElement;
  const choose = (selector: string, value: string) => {
    const select = element.querySelector(selector) as HTMLSelectElement;
    select.value = value;
    select.dispatchEvent(new Event('change'));
  };
  return { element, changes, choose };
}

describe('ActionEditor', () => {
  it('switches the action type to that type’s default', async () => {
    const { changes, choose } = await render({ type: 'mediaKey', key: 'mute' });

    choose('#action-type', 'switchProfile');

    expect(changes).toEqual([{ type: 'switchProfile', profileId: 'p2' }]);
  });

  it('edits a hotkey’s modifiers and key', async () => {
    const { element, changes, choose } = await render({ type: 'hotkey', modifiers: ['ctrl'], key: 'c' });
    const shift = [...element.querySelectorAll('.check')].find((check) => check.textContent?.includes('Shift'));
    const box = shift?.querySelector('input') as HTMLInputElement;

    box.checked = true;
    box.dispatchEvent(new Event('change'));
    choose('#action-key', 'f13');

    expect(changes).toEqual([
      { type: 'hotkey', modifiers: ['ctrl', 'shift'], key: 'c' },
      { type: 'hotkey', modifiers: ['ctrl'], key: 'f13' },
    ]);
  });

  it('picks a specific page', async () => {
    const { element, changes, choose } = await render({ type: 'changePage', target: 'specific', pageIndex: 0 });

    expect(element.querySelectorAll('#action-page option')).toHaveLength(3);
    choose('#action-page', '2');

    expect(changes).toEqual([{ type: 'changePage', target: 'specific', pageIndex: 2 }]);
  });

  it('shows an action it cannot edit yet read-only', async () => {
    const { element } = await render({ type: 'openUrl', url: 'https://example.com' });

    expect(element.querySelector('.readonly')?.textContent).toContain('Open https://example.com');
    expect((element.querySelector('#action-type') as HTMLSelectElement).selectedOptions[0].textContent).toBe(
      'Open URL',
    );
  });
});
