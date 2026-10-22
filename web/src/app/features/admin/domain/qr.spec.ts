import { qrModules } from './qr';

describe('qrModules', () => {
  it('encodes the pairing link as a square module grid with finder patterns', () => {
    const qr = qrModules('http://leverdeck.local:8765/pair?code=472915');

    expect(qr.size).toBeGreaterThanOrEqual(21);
    expect((qr.size - 17) % 4).toBe(0);
    // The top-left finder pattern starts with a 7-module dark row.
    expect(qr.path.startsWith('M0 0h7v1H0z')).toBe(true);
  });

  it('is deterministic', () => {
    expect(qrModules('abc')).toEqual(qrModules('abc'));
    expect(qrModules('abc').path).not.toBe(qrModules('abd').path);
  });
});
