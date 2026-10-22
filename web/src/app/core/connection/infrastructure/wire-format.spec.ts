import type { ExecuteActionCommand, GetProfilesQuery } from '../../protocol';
import { parseIncoming, serializeOutgoing } from './wire-format';

describe('parseIncoming', () => {
  it('parses an ack', () => {
    expect(parseIncoming('{"kind":"ack","requestId":"r1"}')).toEqual({ kind: 'ack', requestId: 'r1' });
  });

  it('parses an error, also when the request id is null', () => {
    const message = parseIncoming(
      '{"kind":"error","requestId":null,"code":"unauthorized","message":"Unknown token."}',
    );

    expect(message).toEqual({
      kind: 'error',
      requestId: null,
      code: 'unauthorized',
      message: 'Unknown token.',
    });
  });

  it('parses a query result', () => {
    const message = parseIncoming(
      '{"kind":"queryResult","type":"profiles","requestId":"r2","payload":{"profiles":[]}}',
    );

    expect(message?.kind).toBe('queryResult');
    expect(message).toMatchObject({ type: 'profiles', payload: { profiles: [] } });
  });

  it('parses an event whatever the field order', () => {
    const message = parseIncoming(
      '{"payload":{"profileId":"p1"},"type":"profileDeleted","kind":"event"}',
    );

    expect(message).toEqual({ kind: 'event', type: 'profileDeleted', payload: { profileId: 'p1' } });
  });

  it.each([
    ['invalid JSON', '{not json'],
    ['a JSON array', '[]'],
    ['a JSON string', '"ack"'],
    ['null', 'null'],
    ['a missing kind', '{"requestId":"r1"}'],
    ['an unknown kind', '{"kind":"surprise","requestId":"r1"}'],
    ['a client-to-agent kind', '{"kind":"command","type":"executeAction","requestId":"r1","payload":{}}'],
    ['an ack without a request id', '{"kind":"ack"}'],
    ['an error without a code', '{"kind":"error","requestId":"r1","message":"x"}'],
    ['a query result without a type', '{"kind":"queryResult","requestId":"r1","payload":{}}'],
    ['an event without a payload', '{"kind":"event","type":"devicesChanged"}'],
  ])('ignores %s', (_case, raw) => {
    expect(parseIncoming(raw)).toBeNull();
  });

  it('ignores frames that are not text', () => {
    expect(parseIncoming(new ArrayBuffer(4))).toBeNull();
  });
});

describe('serializeOutgoing', () => {
  it('writes kind first and type second, as the agent does', () => {
    const command: ExecuteActionCommand = {
      payload: { profileId: 'p1', pageIndex: 0, buttonId: 'b1' },
      requestId: 'r1',
      type: 'executeAction',
      kind: 'command',
    };

    expect(serializeOutgoing(command)).toBe(
      '{"kind":"command","type":"executeAction","requestId":"r1","payload":{"profileId":"p1","pageIndex":0,"buttonId":"b1"}}',
    );
  });

  it('serializes queries with an empty payload', () => {
    const query: GetProfilesQuery = { kind: 'query', type: 'getProfiles', requestId: 'r2', payload: {} };

    expect(serializeOutgoing(query)).toBe(
      '{"kind":"query","type":"getProfiles","requestId":"r2","payload":{}}',
    );
  });
});
