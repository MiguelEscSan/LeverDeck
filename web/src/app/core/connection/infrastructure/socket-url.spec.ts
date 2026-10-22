import { socketUrl } from './socket-url';

describe('socketUrl', () => {
  it('uses ws: on the same host and port for an http page', () => {
    expect(socketUrl({ protocol: 'http:', host: 'leverdeck.local:8765' })).toBe(
      'ws://leverdeck.local:8765/ws',
    );
  });

  it('uses wss: for an https page', () => {
    expect(socketUrl({ protocol: 'https:', host: 'leverdeck.local' })).toBe('wss://leverdeck.local/ws');
  });

  it('works over USB on localhost', () => {
    expect(socketUrl({ protocol: 'http:', host: 'localhost:8765' })).toBe('ws://localhost:8765/ws');
  });
});
