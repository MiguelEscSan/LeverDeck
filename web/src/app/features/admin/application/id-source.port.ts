/** New ids for profiles and keys (UUIDs, as the agent expects). */
export abstract class IdSource {
  abstract next(): string;
}
