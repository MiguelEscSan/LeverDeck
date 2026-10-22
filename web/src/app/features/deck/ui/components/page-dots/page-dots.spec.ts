import { TestBed } from '@angular/core/testing';
import { PageDots } from './page-dots';

describe('PageDots', () => {
  it('marks the current page and emits the chosen one', async () => {
    const fixture = TestBed.createComponent(PageDots);
    fixture.componentRef.setInput('count', 3);
    fixture.componentRef.setInput('current', 1);
    await fixture.whenStable();
    const selected: number[] = [];
    fixture.componentInstance.selected.subscribe((page) => selected.push(page));
    const dots = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];

    expect(dots.map((dot) => dot.textContent?.trim())).toEqual(['1', '2', '3']);
    expect(dots[1].getAttribute('aria-current')).toBe('page');
    dots[2].click();

    expect(selected).toEqual([2]);
  });
});
