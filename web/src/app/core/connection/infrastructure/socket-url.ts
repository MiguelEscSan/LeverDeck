/** The parts of `Location` the socket URL depends on. */
export interface PageLocation {
  readonly protocol: string;
  readonly host: string;
}

export const SOCKET_PATH = '/ws';

/**
 * The agent serves the PWA and the WebSocket on the same origin, so the socket URL follows the page: `ws:` for
 * `http:`, `wss:` for `https:`, same host and port, path `/ws`.
 */
export function socketUrl(location: PageLocation): string {
  const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:';
  return `${scheme}//${location.host}${SOCKET_PATH}`;
}
