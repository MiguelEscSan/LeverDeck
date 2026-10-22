import { TestBed } from '@angular/core/testing';
import type { ButtonDto } from '../../../../../core/protocol';
import { EditorGrid, type GridPosition } from './editor-grid';

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
});
