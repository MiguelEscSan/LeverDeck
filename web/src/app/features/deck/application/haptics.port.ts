/** Short vibrations as press feedback; a no-op where the device cannot vibrate (iOS Safari, desktops). */
export abstract class Haptics {
  /** A key was pressed. */
  abstract tap(): void;

  /** The PC could not run the key's action. */
  abstract failure(): void;
}
