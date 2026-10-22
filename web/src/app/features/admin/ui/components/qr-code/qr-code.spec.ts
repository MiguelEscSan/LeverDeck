import { TestBed } from '@angular/core/testing';
import { qrModules } from '../../../domain/qr';
import { QrCode } from './qr-code';

describe('QrCode', () => {
  it('draws the modules of its text with a quiet zone and an accessible label', async () => {
    const text = 'http://leverdeck.local:8765/pair?code=472915';
    const fixture = TestBed.createComponent(QrCode);
    fixture.componentRef.setInput('text', text);
    fixture.componentRef.setInput('label', 'Pairing QR code');
    await fixture.whenStable();
    const svg = (fixture.nativeElement as HTMLElement).querySelector('svg');
    const { size, path } = qrModules(text);

    expect(svg?.getAttribute('viewBox')).toBe(`-2 -2 ${size + 4} ${size + 4}`);
    expect(svg?.getAttribute('aria-label')).toBe('Pairing QR code');
    expect(svg?.querySelector('path')?.getAttribute('d')).toBe(path);
  });
});
