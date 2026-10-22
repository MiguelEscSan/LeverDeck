import { CryptoIdSource } from './crypto-id-source';

describe('CryptoIdSource', () => {
  it('formats the random bytes it is given as a UUID', () => {
    const source = new CryptoIdSource((bytes) => bytes.fill(0xab));

    expect(source.next()).toBe('abababab-abab-4bab-abab-abababababab');
  });

  it('gives different ids with real randomness', () => {
    const source = new CryptoIdSource((bytes) => crypto.getRandomValues(bytes));

    expect(source.next()).not.toBe(source.next());
  });
});
