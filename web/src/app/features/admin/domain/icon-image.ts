/** Uploaded icons are drawn on a square of this many pixels, as a PNG (the agent accepts up to 512). */
export const ICON_SIDE = 256;

/** The largest picture the editor tries to read; anything bigger is refused before decoding it. */
export const ICON_SOURCE_MAX_BYTES = 10 * 1024 * 1024;

export interface IconPlacement {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

/** Fits a picture inside the icon square, keeping its proportions and centring it (the rest stays transparent). */
export function fitInSquare(width: number, height: number, side: number = ICON_SIDE): IconPlacement {
  if (width <= 0 || height <= 0) {
    return { x: 0, y: 0, width: side, height: side };
  }
  const scale = Math.min(side / width, side / height);
  const fitted = { width: Math.round(width * scale), height: Math.round(height * scale) };
  return { ...fitted, x: Math.round((side - fitted.width) / 2), y: Math.round((side - fitted.height) / 2) };
}

/** Base64 of some bytes, for the `saveIcon` payload. */
export function toBase64(bytes: Uint8Array): string {
  let binary = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return btoa(binary);
}
