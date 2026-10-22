import { uuidFromBytes } from './ids';

describe('uuidFromBytes', () => {
  it('formats a version 4, RFC 4122 variant UUID', () => {
    const id = uuidFromBytes(new Uint8Array(16).fill(0xff));

    expect(id).toBe('ffffffff-ffff-4fff-bfff-ffffffffffff');
    expect(uuidFromBytes(new Uint8Array(16))).toBe('00000000-0000-4000-8000-000000000000');
  });

  it('needs exactly 16 bytes', () => {
    expect(() => uuidFromBytes(new Uint8Array(8))).toThrow('A UUID needs 16 bytes.');
  });
});
