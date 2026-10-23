import { TestBed } from '@angular/core/testing';
import type { ButtonDto } from '../../../../../core/protocol';
import { ICON_PAGE_SIZE, KeyInspector } from './key-inspector';

const play: ButtonDto = {
  id: 'b1',
  row: 0,
  column: 0,
  label: 'Play',
  color: 'blue',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
};

async function render(inputs: { button?: ButtonDto; emptyAt?: { row: number; column: number } }) {
  const fixture = TestBed.createComponent(KeyInspector);
  fixture.componentRef.setInput('button', inputs.button ?? null);
  fixture.componentRef.setInput('emptyAt', inputs.emptyAt ?? null);
  fixture.componentRef.setInput('profileId', 'p1');
  fixture.componentRef.setInput('profiles', [{ id: 'p1', name: 'Streaming', color: 'red' }]);
  fixture.componentRef.setInput('pageCount', 1);
  await fixture.whenStable();
  const events: unknown[] = [];
  fixture.componentInstance.changed.subscribe((change) => events.push(change));
  fixture.componentInstance.actionChanged.subscribe((action) => events.push(action));
  fixture.componentInstance.add.subscribe(() => events.push('add'));
  fixture.componentInstance.remove.subscribe(() => events.push('remove'));
  return { element: fixture.nativeElement as HTMLElement, events };
}

describe('KeyInspector', () => {
  it('edits the label, color, icon and action of the selected key', async () => {
    const { element, events } = await render({ button: play });
    const label = element.querySelector('#key-label') as HTMLInputElement;

    expect(label.value).toBe('Play');
    label.value = 'Pause';
    label.dispatchEvent(new Event('change'));
    (element.querySelector('[aria-label="Orange"]') as HTMLButtonElement).click();
    (element.querySelector('.icon[aria-label="Star"]') as HTMLButtonElement).click();
    const media = element.querySelector('#action-media') as HTMLSelectElement;
    media.value = 'mute';
    media.dispatchEvent(new Event('change'));
    [...element.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Remove key')?.click();

    expect(events).toEqual([
      { label: 'Pause' },
      { color: 'orange' },
      { iconId: 'star' },
      { action: { type: 'mediaKey', key: 'mute' }, name: undefined, previousName: undefined },
      'remove',
    ]);
    expect(element.querySelector('.icon[aria-label="Play/Pause"]')?.getAttribute('aria-checked')).toBe('true');
  });

  it('offers to add a key on an empty position', async () => {
    const { element, events } = await render({ emptyAt: { row: 1, column: 2 } });

    expect(element.querySelector('.hint')?.textContent).toContain('Row 2, column 3 is empty.');
    [...element.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Add a key here')?.click();

    expect(events).toEqual(['add']);
  });

  it('explains what to do with nothing selected', async () => {
    const { element } = await render({});

    expect(element.querySelector('.hint')?.textContent).toContain('Choose a key on the grid');
  });
});

describe('KeyInspector icon paging', () => {
  it('shows one page of icons and a pager when there are many uploads', async () => {
    const fixture = TestBed.createComponent(KeyInspector);
    fixture.componentRef.setInput('button', play);
    fixture.componentRef.setInput('profileId', 'p1');
    fixture.componentRef.setInput('profiles', [{ id: 'p1', name: 'Streaming', color: 'red' }]);
    fixture.componentRef.setInput('pageCount', 1);
    fixture.componentRef.setInput(
      'customIcons',
      Array.from({ length: 30 }, (_, index) => `custom-${index.toString(16).padStart(32, '0')}`),
    );
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelectorAll('.icons .icon')).toHaveLength(ICON_PAGE_SIZE);
    expect(element.querySelector('.icon-pager')?.textContent).toContain(`1 / ${Math.ceil((30 + 30) / ICON_PAGE_SIZE)}`);
  });
});
