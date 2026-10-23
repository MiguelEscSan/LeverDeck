import { SoundTransport } from '../application/sound-transport.port';

/** The HTTP routes of a sound clip (`/api/sounds/<id>.wav`), with the admin token in the `X-LeverDeck-Admin` header. */
export const ADMIN_TOKEN_HEADER = 'X-LeverDeck-Admin';

type Fetch = (input: string, init?: RequestInit) => Promise<Response>;

/**
 * The browser's implementation: `PUT` to upload, `GET` to fetch a clip for its preview. The preview plays through an
 * `Audio` element on a blob URL that is released once the clip has played.
 */
export class HttpSoundTransport extends SoundTransport {
  constructor(
    private readonly token: () => string | null,
    private readonly fetcher: Fetch = (input, init) => fetch(input, init),
    private readonly playBlob: (blob: Blob) => Promise<void> = playOnce,
  ) {
    super();
  }

  async upload(soundId: string, name: string, file: Blob): Promise<void> {
    const response = await this.fetcher(`/api/sounds/${soundId}.wav?name=${encodeURIComponent(name)}`, {
      method: 'PUT',
      headers: this.headers({ 'Content-Type': 'audio/wav' }),
      body: file,
    });
    if (!response.ok) {
      throw new Error((await response.text()).trim() || "Couldn't upload the sound. Try again.");
    }
  }

  async preview(soundId: string): Promise<void> {
    const response = await this.fetcher(`/api/sounds/${soundId}.wav`, { headers: this.headers() });
    if (!response.ok) {
      throw new Error("Couldn't load the sound. It may have been removed.");
    }
    await this.playBlob(await response.blob());
  }

  private headers(extra: Record<string, string> = {}): Record<string, string> {
    return { [ADMIN_TOKEN_HEADER]: this.token() ?? '', ...extra };
  }
}

async function playOnce(blob: Blob): Promise<void> {
  const url = URL.createObjectURL(blob);
  const audio = new Audio(url);
  audio.addEventListener('ended', () => URL.revokeObjectURL(url), { once: true });
  await audio.play();
}
