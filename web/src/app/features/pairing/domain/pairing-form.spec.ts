import {
  defaultDeviceName,
  isCompleteCode,
  normalizeCode,
  normalizeDeviceName,
  redeemErrorMessage,
  rejectionNotice,
} from './pairing-form';

describe('normalizeCode', () => {
  it('keeps at most six ASCII digits and drops everything else', () => {
    expect(normalizeCode(' 47-29 15 9')).toBe('472915');
    expect(normalizeCode('abc')).toBe('');
    expect(normalizeCode('٤٧٢')).toBe('');
  });

  it('accepts a missing value', () => {
    expect(normalizeCode(null)).toBe('');
  });
});

describe('isCompleteCode', () => {
  it('needs exactly six digits', () => {
    expect(isCompleteCode('472915')).toBe(true);
    expect(isCompleteCode('47291')).toBe(false);
  });
});

describe('defaultDeviceName', () => {
  it.each([
    [
      'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36',
      'Pixel 8',
    ],
    [
      'Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36',
      'Android phone',
    ],
    [
      'Mozilla/5.0 (Linux; Android 13; SM-X700) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36',
      'SM-X700',
    ],
    ['Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15', 'iPhone'],
    ['Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15', 'iPad'],
    ['Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36', 'Windows PC'],
    ['Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15', 'Mac'],
    ['', 'My device'],
  ])('names %s "%s"', (userAgent, name) => {
    expect(defaultDeviceName(userAgent)).toBe(name);
  });

  it('never exceeds the 40 characters the agent accepts', () => {
    const model = 'X'.repeat(80);

    expect(defaultDeviceName(`Mozilla/5.0 (Linux; Android 14; ${model}) Chrome`).length).toBe(40);
  });
});

describe('normalizeDeviceName', () => {
  it('trims and caps the name at 40 characters', () => {
    expect(normalizeDeviceName('  Pixel 8  ')).toBe('Pixel 8');
    expect(normalizeDeviceName('y'.repeat(50))).toHaveLength(40);
  });
});

describe('redeemErrorMessage', () => {
  it('explains a wrong code', () => {
    expect(redeemErrorMessage('pairingCodeInvalid')).toMatch(/not right/);
  });

  it('explains an expired code and how to get a new one', () => {
    expect(redeemErrorMessage('pairingCodeExpired')).toMatch(/expired.*new one/);
  });

  it('explains an unreachable PC', () => {
    expect(redeemErrorMessage('network')).toMatch(/Can.t reach your PC/);
  });

  it('has a generic fallback', () => {
    expect(redeemErrorMessage('internal')).toMatch(/Try again/);
  });
});

describe('rejectionNotice', () => {
  it('explains why the device is back on the pairing screen', () => {
    expect(rejectionNotice('deviceRevoked')).toMatch(/removed/);
    expect(rejectionNotice('unauthorized')).toMatch(/no longer paired/);
    expect(rejectionNotice('other')).toBeNull();
    expect(rejectionNotice(null)).toBeNull();
  });
});
