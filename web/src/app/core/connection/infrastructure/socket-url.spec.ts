import { adminSocketUrl, socketUrl } from './socket-url';

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

describe('adminSocketUrl', () => {
  it('puts the encoded admin token in the query string of /admin/ws', () => {
    expect(adminSocketUrl({ protocol: 'http:', host: 'localhost:8765' }, 'a b/+=')).toBe(
      'ws://localhost:8765/admin/ws?token=a%20b%2F%2B%3D',
    );
  });
});
