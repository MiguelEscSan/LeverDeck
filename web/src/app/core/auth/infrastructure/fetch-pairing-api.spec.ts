import { FetchPairingApi, REDEEM_PATH } from './fetch-pairing-api';

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

describe('FetchPairingApi', () => {
  it('posts the code and device name as JSON and returns the token', async () => {
    const calls: [string, RequestInit][] = [];
    const api = new FetchPairingApi(async (input, init) => {
      calls.push([input, init]);
      return json(200, { deviceId: 'd1', deviceToken: 'secret' });
    });

    const outcome = await api.redeem({ code: '472915', deviceName: 'Pixel 8' });

    expect(outcome).toEqual({ ok: true, response: { deviceId: 'd1', deviceToken: 'secret' } });
    const [url, init] = calls[0];
    expect(url).toBe(REDEEM_PATH);
    expect(init.method).toBe('POST');
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json');
    expect(JSON.parse(init.body as string)).toEqual({ code: '472915', deviceName: 'Pixel 8' });
  });

  it.each(['pairingCodeInvalid', 'pairingCodeExpired', 'validationFailed'])(
    'returns the %s error code of a refusal',
    async (code) => {
      const api = new FetchPairingApi(async () => json(400, { code, message: 'No.' }));

      expect(await api.redeem({ code: '000000', deviceName: 'x' })).toEqual({ ok: false, reason: code });
    },
  );

  it('reports a network failure', async () => {
    const api = new FetchPairingApi(async () => {
      throw new TypeError('Failed to fetch');
    });

    expect(await api.redeem({ code: '000000', deviceName: 'x' })).toEqual({ ok: false, reason: 'network' });
  });

  it('treats an unreadable answer as internal', async () => {
    const api = new FetchPairingApi(async () => new Response('<html>', { status: 502 }));

    expect(await api.redeem({ code: '000000', deviceName: 'x' })).toEqual({ ok: false, reason: 'internal' });
  });
});
