/**
 * What the UI shows about the link to the agent.
 *
 * - `connected`: the socket is open.
 * - `reconnecting`: a socket is opening, or the next attempt is scheduled.
 * - `offline`: not trying — the device has no network, or the connection was stopped.
 */
export type ConnectionStatus = 'connected' | 'reconnecting' | 'offline';
