import { formatCountdown, lastSeenLabel, secondsLeft } from './admin-time';

describe('secondsLeft', () => {
  const expiresAt = '2026-10-22T08:35:00.000Z';
  const at = (iso: string) => Date.parse(iso);

  it('counts whole seconds down to the expiry, rounding up', () => {
    expect(secondsLeft(expiresAt, at('2026-10-22T08:30:00.000Z'))).toBe(300);
    expect(secondsLeft(expiresAt, at('2026-10-22T08:34:59.200Z'))).toBe(1);
  });

  it('never goes below zero', () => {
    expect(secondsLeft(expiresAt, at('2026-10-22T08:40:00.000Z'))).toBe(0);
  });

  it('treats an unreadable date as expired', () => {
    expect(secondsLeft('soon', 0)).toBe(0);
  });
});

describe('formatCountdown', () => {
  it('shows minutes and two-digit seconds', () => {
    expect(formatCountdown(272)).toBe('4:32');
    expect(formatCountdown(65)).toBe('1:05');
    expect(formatCountdown(0)).toBe('0:00');
  });
});

describe('lastSeenLabel', () => {
  const now = Date.parse('2026-10-22T12:00:00.000Z');

  it.each([
    ['2026-10-22T11:59:30.000Z', 'Last seen just now'],
    ['2026-10-22T11:55:00.000Z', 'Last seen 5 min ago'],
    ['2026-10-22T10:00:00.000Z', 'Last seen 2 h ago'],
    ['2026-10-19T12:00:00.000Z', 'Last seen 3 d ago'],
  ])('describes %s as "%s"', (lastSeenAt, label) => {
    expect(lastSeenLabel(lastSeenAt, now)).toBe(label);
  });
});
