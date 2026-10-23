/**
 * Moves sound clips over HTTP, where a clip fits: the admin socket is too small for a 50 MB file. Both calls carry the
 * editor's admin token; a phone cannot reach them.
 */
export abstract class SoundTransport {
  /** Uploads a WAV as a new clip; rejects with the agent's reason when it refuses the file. */
  abstract upload(soundId: string, name: string, file: Blob): Promise<void>;

  /** Plays a stored clip on the editor's own speakers, so the owner can hear it before using it. */
  abstract preview(soundId: string): Promise<void>;
}
