import { TestBed } from '@angular/core/testing';
import { KeyGlyph } from './key-glyph';

describe('KeyGlyph', () => {
  const render = async (iconId: string, label = '') => {
    const fixture = TestBed.createComponent(KeyGlyph);
    fixture.componentRef.setInput('iconId', iconId);
    fixture.componentRef.setInput('label', label);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  };

  it('draws the seeded media icons', async () => {
    for (const id of ['play-pause', 'skip-next', 'skip-previous', 'volume-up', 'volume-down', 'volume-mute']) {
      expect((await render(id)).querySelector('svg')).not.toBeNull();
    }
  });

  it('falls back to the first letters of the label, then of the icon id', async () => {
    expect((await render('obs-scene', 'Just chatting')).textContent?.trim()).toBe('JU');
    expect((await render('obs-scene')).textContent?.trim()).toBe('OB');
  });

  it('shows an uploaded icon from the agent', async () => {
    const image = (await render('custom-0123456789abcdef0123456789abcdef', 'Steam')).querySelector('img');

    expect(image?.getAttribute('src')).toBe('/api/icons/custom-0123456789abcdef0123456789abcdef.png');
    expect(image?.getAttribute('alt')).toBe('');
  });
});
