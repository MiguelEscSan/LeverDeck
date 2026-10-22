import { CanvasIconEncoder } from './canvas-icon-encoder';

/** A canvas that records what is drawn; jsdom has no 2D context. */
function fakeCanvas(draws: number[][]) {
  const canvas = {
    width: 0,
    height: 0,
    getContext: () => ({
      imageSmoothingQuality: 'low',
      drawImage: (_image: unknown, ...box: number[]) => draws.push(box),
    }),
    toBlob: (done: (blob: Blob | null) => void) => done(new Blob([new Uint8Array([0x89, 0x50, 0x4e, 0x47])])),
  };
  return canvas as unknown as HTMLCanvasElement;
}

const bitmap = (width: number, height: number) => ({ width, height, close: () => undefined }) as ImageBitmap;

describe('CanvasIconEncoder', () => {
  it('draws the picture fitted in a 256 px square and returns the PNG as base64', async () => {
    const draws: number[][] = [];
    const encoder = new CanvasIconEncoder(async () => bitmap(512, 256), () => fakeCanvas(draws));

    expect(await encoder.encode(new Blob(['x']))).toBe('iVBORw==');
    expect(draws).toEqual([[0, 64, 256, 128]]);
  });

  it('refuses a file the browser cannot decode', async () => {
    const encoder = new CanvasIconEncoder(
      () => Promise.reject(new Error('bad')),
      () => fakeCanvas([]),
    );

    await expect(encoder.encode(new Blob(['x']))).rejects.toThrow("That file isn't a picture this browser can read.");
  });

  it('refuses a picture over 10 MB without reading it', async () => {
    let decoded = false;
    const encoder = new CanvasIconEncoder(
      async () => {
        decoded = true;
        return bitmap(1, 1);
      },
      () => fakeCanvas([]),
    );

    await expect(encoder.encode(new Blob([new Uint8Array(10 * 1024 * 1024 + 1)]))).rejects.toThrow('too big');
    expect(decoded).toBe(false);
  });
});
