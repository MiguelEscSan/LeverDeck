import { TestBed } from '@angular/core/testing';
import { IconGallery } from './icon-gallery';

const icon = 'custom-0123456789abcdef0123456789abcdef';

async function render(icons: string[]) {
  const fixture = TestBed.createComponent(IconGallery);
  fixture.componentRef.setInput('icons', icons);
  await fixture.whenStable();
  const uploads: string[] = [];
  const removed: string[] = [];
  fixture.componentInstance.upload.subscribe((file) => uploads.push(file.name));
  fixture.componentInstance.remove.subscribe((id) => removed.push(id));
  return { fixture, element: fixture.nativeElement as HTMLElement, uploads, removed };
}

describe('IconGallery', () => {
  it('shows the uploaded icons from the agent', async () => {
    const { element } = await render([icon]);

    expect(element.querySelector('img')?.getAttribute('src')).toBe(`/api/icons/${icon}.png`);
  });

  it('uploads a chosen file', async () => {
    const { element, uploads } = await render([]);
    const field = element.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(field, 'files', { value: [new File(['x'], 'steam.png')] });

    field.dispatchEvent(new Event('change'));

    expect(uploads).toEqual(['steam.png']);
    expect(element.textContent).toContain('No icons yet.');
  });

  it('uploads a file dropped on it', async () => {
    const { element, uploads } = await render([]);
    const files = [new File(['x'], 'mickey.jpg')];
    const event = (type: string) =>
      Object.assign(new Event(type, { bubbles: true, cancelable: true }), {
        dataTransfer: { types: ['Files'], files, dropEffect: 'none' },
      });
    const zone = element.querySelector('.drop') as HTMLElement;

    const over = event('dragover');
    zone.dispatchEvent(over);
    zone.dispatchEvent(event('drop'));

    expect(over.defaultPrevented).toBe(true);
    expect(uploads).toEqual(['mickey.jpg']);
  });

  it('deletes only after confirming', async () => {
    const { fixture, element, removed } = await render([icon]);
    const button = (label: string) =>
      [...element.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === label);

    button('Delete')?.click();
    await fixture.whenStable();
    expect(removed).toEqual([]);
    button('Delete?')?.click();

    expect(removed).toEqual([icon]);
  });
});
