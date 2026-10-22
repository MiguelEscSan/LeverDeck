import qrcode from 'qrcode-generator';

/** A QR code as an SVG path of its dark modules, in module units (no quiet zone; the renderer adds it). */
export interface QrModules {
  readonly size: number;
  readonly path: string;
}

/**
 * Encodes `text` (the pairing link) with medium error correction and the smallest version that fits. Each row's
 * runs of dark modules become one rectangle, which keeps the path short and crisp at any scale.
 */
export function qrModules(text: string): QrModules {
  const qr = qrcode(0, 'M');
  qr.addData(text, 'Byte');
  qr.make();
  const size = qr.getModuleCount();
  let path = '';
  for (let row = 0; row < size; row++) {
    let column = 0;
    while (column < size) {
      if (!qr.isDark(row, column)) {
        column++;
        continue;
      }
      const start = column;
      while (column < size && qr.isDark(row, column)) {
        column++;
      }
      path += `M${start} ${row}h${column - start}v1H${start}z`;
    }
  }
  return { size, path };
}
