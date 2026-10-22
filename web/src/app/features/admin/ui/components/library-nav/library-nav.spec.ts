import { TestBed } from '@angular/core/testing';
import { LibraryNav, type LibrarySection } from './library-nav';

describe('LibraryNav', () => {
  it('shows the counts, marks the open section and emits the chosen one', async () => {
    const fixture = TestBed.createComponent(LibraryNav);
    fixture.componentRef.setInput('current', 'icons');
    fixture.componentRef.setInput('appCount', 2);
    fixture.componentRef.setInput('iconCount', 5);
    await fixture.whenStable();
    const chosen: LibrarySection[] = [];
    fixture.componentInstance.selected.subscribe((section) => chosen.push(section));
    const items = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];

    expect(items.map((item) => item.textContent?.replace(/\s+/g, ' ').trim())).toEqual(['Apps 2', 'Icons 5']);
    expect(items.map((item) => item.getAttribute('aria-current'))).toEqual([null, 'page']);
    items[0].click();

    expect(chosen).toEqual(['apps']);
  });
});
