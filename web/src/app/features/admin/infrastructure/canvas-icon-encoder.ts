import { IconEncoder } from '../application/icon-encoder.port';
import { fitInSquare, ICON_SIDE, ICON_SOURCE_MAX_BYTES, toBase64 } from '../domain/icon-image';

/**
 * Draws the picture (PNG, JPEG, WebP, GIF… whatever the browser decodes) centred on a transparent 256 × 256 canvas
 * and exports it as PNG. Re-encoding also drops anything but pixels, so only a plain image ever reaches the agent.
 */
export class CanvasIconEncoder extends IconEncoder {
  constructor(
    private readonly decode: (file: Blob) => Promise<ImageBitmap>,
    private readonly createCanvas: () => HTMLCanvasElement,
  ) {
    super();
  }

  async encode(file: Blob): Promise<string> {
    if (file.size > ICON_SOURCE_MAX_BYTES) {
      throw new Error('That picture is too big. Use one under 10 MB.');
    }
    let bitmap: ImageBitmap;
    try {
      bitmap = await this.decode(file);
    } catch {
      throw new Error("That file isn't a picture this browser can read. Try a PNG or JPG.");
    }
    try {
      const canvas = this.createCanvas();
      canvas.width = ICON_SIDE;
      canvas.height = ICON_SIDE;
      const context = canvas.getContext('2d');
      if (!context) {
        throw new Error("This browser can't draw the icon.");
      }
      const place = fitInSquare(bitmap.width, bitmap.height);
      context.imageSmoothingQuality = 'high';
      context.drawImage(bitmap, place.x, place.y, place.width, place.height);
      const png = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/png'));
      if (!png) {
        throw new Error("This browser can't draw the icon.");
      }
      return toBase64(new Uint8Array(await png.arrayBuffer()));
    } finally {
      bitmap.close();
    }
  }
}
