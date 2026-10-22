import { IdSource } from '../application/id-source.port';
import { uuidFromBytes } from '../domain/ids';

/** UUIDs from `crypto.getRandomValues`, which (unlike `crypto.randomUUID`) works outside a secure context. */
export class CryptoIdSource extends IdSource {
  /** `fillRandom` fills the array it is given in place, as `crypto.getRandomValues` does. */
  constructor(private readonly fillRandom: (bytes: Uint8Array<ArrayBuffer>) => unknown) {
    super();
  }

  next(): string {
    const bytes = new Uint8Array(16);
    this.fillRandom(bytes);
    return uuidFromBytes(bytes);
  }
}
