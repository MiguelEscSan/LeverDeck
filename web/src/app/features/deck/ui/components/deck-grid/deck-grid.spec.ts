import { TestBed } from '@angular/core/testing';
import type { ButtonDto } from '../../../../../core/protocol';
import type { DeckCell } from '../../../domain/deck';
import { DeckGrid } from './deck-grid';

const play: ButtonDto = {
  id: 'play',
  row: 0,
  column: 1,
  label: 'Play/Pause',
  color: 'orange',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
};

const cells: DeckCell[] = [
  { row: 0, column: 0, button: null },
  { row: 0, column: 1, button: play },
];

describe('DeckGrid', () => {
  const render = async (feedback = {}) => {
    const fixture = TestBed.createComponent(DeckGrid);
    fixture.componentRef.setInput('cells', cells);
    fixture.componentRef.setInput('columns', 2);
    fixture.componentRef.setInput('rows', 1);
    fixture.componentRef.setInput('feedback', feedback);
    await fixture.whenStable();
    const pressed: ButtonDto[] = [];
    fixture.componentInstance.pressed.subscribe((button) => pressed.push(button));
    return { element: fixture.nativeElement as HTMLElement, pressed };
  };

  it('renders keys and quiet empty slots in grid order', async () => {
    const { element } = await render();

    expect(element.querySelectorAll('.empty')).toHaveLength(1);
    const key = element.querySelector('button');
    expect(key?.getAttribute('aria-label')).toBe('Play/Pause');
    expect(key?.getAttribute('data-color')).toBe('orange');
    expect(key?.classList).toContain('icon-only');
    expect(key?.querySelector('.label')).toBeNull();
    expect(element.querySelector('svg path')).not.toBeNull();
  });

  it('emits the pressed button', async () => {
    const { element, pressed } = await render();

    element.querySelector('button')?.click();

    expect(pressed).toEqual([play]);
  });

  it('shows the press feedback of a key', async () => {
    const { element } = await render({ play: 'failure' });

    expect(element.querySelector('button')?.getAttribute('data-feedback')).toBe('failure');
  });

  it('sizes the columns from the grid size', async () => {
    const { element } = await render();

    expect(element.style.getPropertyValue('--columns')).toBe('2');
    expect(element.style.getPropertyValue('--rows')).toBe('1');
  });
});
