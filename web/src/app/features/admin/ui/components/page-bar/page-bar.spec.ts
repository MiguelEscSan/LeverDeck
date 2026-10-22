import { TestBed } from '@angular/core/testing';
import { PageBar } from './page-bar';

describe('PageBar', () => {
  it('lists the pages, marks the current one and emits choices', async () => {
    const fixture = TestBed.createComponent(PageBar);
    fixture.componentRef.setInput('count', 2);
    fixture.componentRef.setInput('current', 1);
    await fixture.whenStable();
    const events: string[] = [];
    fixture.componentInstance.selected.subscribe((page) => events.push(`page ${page}`));
    fixture.componentInstance.add.subscribe(() => events.push('add'));
    fixture.componentInstance.remove.subscribe(() => events.push('remove'));
    const element = fixture.nativeElement as HTMLElement;
    const pages = [...element.querySelectorAll('.page')] as HTMLButtonElement[];

    expect(pages.map((page) => page.textContent?.trim())).toEqual(['1', '2', '+']);
    expect(pages[1].getAttribute('aria-current')).toBe('page');
    pages[0].click();
    pages[2].click();
    (element.querySelector('.remove') as HTMLButtonElement).click();

    expect(events).toEqual(['page 0', 'add', 'remove']);
  });

  it('keeps the last page', async () => {
    const fixture = TestBed.createComponent(PageBar);
    fixture.componentRef.setInput('count', 1);
    fixture.componentRef.setInput('current', 0);
    await fixture.whenStable();

    expect(((fixture.nativeElement as HTMLElement).querySelector('.remove') as HTMLButtonElement).disabled).toBe(true);
  });
});
