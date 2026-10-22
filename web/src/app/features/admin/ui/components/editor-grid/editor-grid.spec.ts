import { TestBed } from '@angular/core/testing';
import type { ButtonDto } from '../../../../../core/protocol';
import { EditorGrid, type GridPosition, KEY_DRAG_TYPE, type KeyMove } from './editor-grid';

const play: ButtonDto = {
  id: 'b1',
  row: 0,
  column: 1,
  label: '',
  color: 'blue',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
};

describe('EditorGrid', () => {
  it('names every position, marks the selected one and emits the chosen one', async () => {
    const fixture = TestBed.createComponent(EditorGrid);
    fixture.componentRef.setInput('cells', [
      { row: 0, column: 0, button: null },
      { row: 0, column: 1, button: play },
    ]);
    fixture.componentRef.setInput('columns', 2);
    fixture.componentRef.setInput('selection', { row: 0, column: 1 });
    await fixture.whenStable();
    const chosen: GridPosition[] = [];
    fixture.componentInstance.selected.subscribe((position) => chosen.push(position));
    const cells = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];

    expect(cells.map((cell) => cell.getAttribute('aria-label'))).toEqual([
      'Empty, row 1, column 1',
      'Media key, row 1, column 2',
    ]);
    expect(cells.map((cell) => cell.getAttribute('aria-pressed'))).toEqual(['false', 'true']);
    expect(cells[1].querySelector('app-key-face svg')).not.toBeNull();

    cells[0].click();
    expect(chosen).toEqual([{ row: 0, column: 0 }]);
  });

  describe('moving keys', () => {
    /** A drag event with a minimal DataTransfer, which jsdom does not provide. */
    const drag = (type: string, data: Map<string, string>) =>
      Object.assign(new Event(type, { bubbles: true, cancelable: true }), {
        dataTransfer: {
          get types() {
            return [...data.keys()];
          },
          setData: (format: string, value: string) => data.set(format, value),
          getData: (format: string) => data.get(format) ?? '',
          effectAllowed: 'none',
          dropEffect: 'none',
        },
      });

    async function renderGrid() {
      const fixture = TestBed.createComponent(EditorGrid);
      fixture.componentRef.setInput('cells', [
        { row: 0, column: 0, button: null },
        { row: 0, column: 1, button: play },
        { row: 1, column: 0, button: null },
        { row: 1, column: 1, button: null },
      ]);
      fixture.componentRef.setInput('columns', 2);
      await fixture.whenStable();
      const moves: KeyMove[] = [];
      fixture.componentInstance.moved.subscribe((move) => moves.push(move));
      return { fixture, moves, cells: [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')] };
    }

    it('drags a key onto another position', async () => {
      const { fixture, moves, cells } = await renderGrid();
      const data = new Map<string, string>();

      expect(cells[1].getAttribute('draggable')).toBe('true');
      expect(cells[0].getAttribute('draggable')).toBeNull();
      cells[1].dispatchEvent(drag('dragstart', data));
      expect(data.get(KEY_DRAG_TYPE)).toBe('b1');
      const over = drag('dragover', data);
      cells[2].dispatchEvent(over);
      await fixture.whenStable();
      expect(over.defaultPrevented).toBe(true);
      expect(cells[2].classList).toContain('drop-target');
      cells[2].dispatchEvent(drag('drop', data));

      expect(moves).toEqual([{ buttonId: 'b1', row: 1, column: 0 }]);
    });

    it('ignores drags that are not keys', async () => {
      const { moves, cells } = await renderGrid();
      const over = drag('dragover', new Map([['text/plain', 'hello']]));

      cells[0].dispatchEvent(over);
      cells[0].dispatchEvent(drag('drop', new Map([['text/plain', 'hello']])));

      expect(over.defaultPrevented).toBe(false);
      expect(moves).toEqual([]);
    });

    it('moves a key with Alt and an arrow key, inside the grid only', async () => {
      const { moves, cells } = await renderGrid();
      const press = (key: string, altKey = true) =>
        cells[1].dispatchEvent(new KeyboardEvent('keydown', { key, altKey, bubbles: true }));

      press('ArrowDown');
      press('ArrowRight');
      press('ArrowLeft', false);
      press('ArrowLeft');

      expect(moves).toEqual([
        { buttonId: 'b1', row: 1, column: 1 },
        { buttonId: 'b1', row: 0, column: 0 },
      ]);
    });
  });
});
