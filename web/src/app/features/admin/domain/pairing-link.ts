/** The host a phone opens to type the code (`leverdeck-<pc>.local:8765`), read from the pairing link; `null` if unreadable. */
export function pairingHost(url: string): string | null {
  try {
    return new URL(url).host || null;
  } catch {
    return null;
  }
}
