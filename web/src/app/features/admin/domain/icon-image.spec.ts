import { fitInSquare, toBase64 } from './icon-image';

describe('fitInSquare', () => {
  it('scales a wide picture to the width and centres it vertically', () => {
    expect(fitInSquare(1000, 500)).toEqual({ x: 0, y: 64, width: 256, height: 128 });
  });

  it('scales a tall picture to the height and centres it horizontally', () => {
    expect(fitInSquare(100, 400)).toEqual({ x: 96, y: 0, width: 64, height: 256 });
  });

  it('enlarges a small square picture to fill the square', () => {
    expect(fitInSquare(32, 32)).toEqual({ x: 0, y: 0, width: 256, height: 256 });
  });

  it('fills the square for an unreadable size', () => {
    expect(fitInSquare(0, 10)).toEqual({ x: 0, y: 0, width: 256, height: 256 });
  });
});

describe('toBase64', () => {
  it('encodes bytes, also beyond one chunk', () => {
    expect(toBase64(new Uint8Array([0x89, 0x50, 0x4e, 0x47]))).toBe('iVBORw==');
    const big = new Uint8Array(70_000).fill(65);
    expect(atob(toBase64(big))).toHaveLength(70_000);
  });
});
