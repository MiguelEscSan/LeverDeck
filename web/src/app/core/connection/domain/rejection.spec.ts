import { rejectionFromClose, rejectionFromError } from './rejection';

describe('rejectionFromClose', () => {
  it.each(['unauthorized', 'deviceRevoked', 'protocolMismatch'] as const)(
    'reads 1008 %s as a rejection',
    (reason) => {
      expect(rejectionFromClose(1008, reason)).toBe(reason);
    },
  );

  it.each([
    [1006, ''],
    [1001, 'shutdown'],
    [1009, 'validationFailed'],
    [1011, 'internal'],
    [1000, ''],
    [1008, 'validationFailed'],
    [4000, 'unauthorized'],
  ])('treats %i "%s" as worth reconnecting', (code, reason) => {
    expect(rejectionFromClose(code, reason)).toBeNull();
  });
});

describe('rejectionFromError', () => {
  it('reads the handshake error codes that end the connection', () => {
    expect(rejectionFromError('unauthorized')).toBe('unauthorized');
    expect(rejectionFromError('deviceRevoked')).toBe('deviceRevoked');
    expect(rejectionFromError('protocolMismatch')).toBe('protocolMismatch');
    expect(rejectionFromError('internal')).toBeNull();
  });
});
