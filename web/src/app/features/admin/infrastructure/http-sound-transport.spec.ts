import { ADMIN_TOKEN_HEADER, HttpSoundTransport } from './http-sound-transport';

interface Sent {
  readonly url: string;
  readonly init: RequestInit | undefined;
}

/** A `fetch` that answers with the given response and records each request. */
function fetcher(respond: () => Response) {
  const sent: Sent[] = [];
  const fetch = async (url: string, init?: RequestInit): Promise<Response> => {
    sent.push({ url, init });
    return respond();
  };
  return { fetch, sent };
}

describe('HttpSoundTransport', () => {
  const token = 'admin-token-value';

  it('uploads the WAV with the admin token, its type and its name in the query', async () => {
    const { fetch, sent } = fetcher(() => new Response(null, { status: 204 }));
    const transport = new HttpSoundTransport(() => token, fetch);

    await transport.upload('sound-0123456789abcdef0123456789abcdef', 'Applause Short', new Blob(['RIFF']));

    expect(sent).toHaveLength(1);
    expect(sent[0].url).toBe('/api/sounds/sound-0123456789abcdef0123456789abcdef.wav?name=Applause%20Short');
    expect(sent[0].init?.method).toBe('PUT');
    expect(new Headers(sent[0].init?.headers).get(ADMIN_TOKEN_HEADER)).toBe(token);
    expect(new Headers(sent[0].init?.headers).get('Content-Type')).toBe('audio/wav');
  });

  it('rejects an upload with the reason the agent sends', async () => {
    const { fetch } = fetcher(() => new Response('A sound must be at most 50 MB.', { status: 413 }));
    const transport = new HttpSoundTransport(() => token, fetch);

    await expect(transport.upload('sound-0123456789abcdef0123456789abcdef', 'Big', new Blob([]))).rejects.toThrow(
      'A sound must be at most 50 MB.',
    );
  });

  it('fetches a clip with the admin token and plays it', async () => {
    const { fetch, sent } = fetcher(() => new Response(new Blob(['RIFF']), { status: 200 }));
    const played: Blob[] = [];
    const transport = new HttpSoundTransport(() => token, fetch, async (blob) => {
      played.push(blob);
    });

    await transport.preview('sound-0123456789abcdef0123456789abcdef');

    expect(sent[0].url).toBe('/api/sounds/sound-0123456789abcdef0123456789abcdef.wav');
    expect(new Headers(sent[0].init?.headers).get(ADMIN_TOKEN_HEADER)).toBe(token);
    expect(played).toHaveLength(1);
  });

  it('says a clip is gone when the agent has no such file', async () => {
    const { fetch } = fetcher(() => new Response(null, { status: 404 }));
    const transport = new HttpSoundTransport(() => token, fetch);

    await expect(transport.preview('sound-0123456789abcdef0123456789abcdef')).rejects.toThrow('It may have been removed.');
  });
});
