import { pairingHost } from './pairing-link';

describe('pairingHost', () => {
  it('reads the host and port of the pairing link', () => {
    expect(pairingHost('http://leverdeck.local:8765/pair?code=472915')).toBe('leverdeck.local:8765');
    expect(pairingHost('http://192.168.1.20:8765/pair?code=472915')).toBe('192.168.1.20:8765');
  });

  it('gives null for an unreadable link', () => {
    expect(pairingHost('not a url')).toBeNull();
  });
});
