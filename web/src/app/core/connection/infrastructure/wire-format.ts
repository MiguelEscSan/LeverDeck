import type { IncomingMessage, OutgoingMessage } from '../domain/messages';

type JsonObject = Record<string, unknown>;

const isObject = (value: unknown): value is JsonObject =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const isString = (value: unknown): value is string => typeof value === 'string';

/** Envelope checks per incoming `kind` (ROADMAP 3.2). Payload shapes are trusted: the agent generates them. */
const envelopeIsValid: Record<IncomingMessage['kind'], (message: JsonObject) => boolean> = {
  ack: (m) => isString(m['requestId']) && (m['payload'] === undefined || isObject(m['payload'])),
  error: (m) =>
    (isString(m['requestId']) || m['requestId'] === null) &&
    isString(m['code']) &&
    isString(m['message']),
  queryResult: (m) => isString(m['requestId']) && isString(m['type']) && isObject(m['payload']),
  event: (m) => isString(m['type']) && isObject(m['payload']),
};

const isIncomingKind = (kind: unknown): kind is IncomingMessage['kind'] =>
  isString(kind) && Object.hasOwn(envelopeIsValid, kind);

/**
 * Reads one WebSocket frame from the agent. Returns `null` for anything that is not a well-formed agent-to-device
 * envelope (binary frames, invalid JSON, unknown or client-side kinds, missing fields), so a bad frame never breaks
 * the connection.
 */
export function parseIncoming(data: unknown): IncomingMessage | null {
  if (!isString(data)) {
    return null;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(data);
  } catch {
    return null;
  }

  if (
    !isObject(parsed) ||
    !isIncomingKind(parsed['kind']) ||
    !envelopeIsValid[parsed['kind']](parsed)
  ) {
    return null;
  }

  return parsed as unknown as IncomingMessage;
}

/** Writes a device message in the agent's field order: `kind`, `type`, `requestId`, `payload`. */
export function serializeOutgoing(message: OutgoingMessage): string {
  const { kind, type, requestId, payload } = message;
  return JSON.stringify({ kind, type, requestId, payload });
}
