import { DEFAULT_BACKOFF, reconnectDelay } from './backoff';

describe('reconnectDelay', () => {
  const maxRandom = () => 1;
  const minRandom = () => 0;

  it('doubles from the base delay on every attempt', () => {
    const delays = [0, 1, 2, 3, 4].map((attempt) => reconnectDelay(attempt, maxRandom));

    expect(delays).toEqual([500, 1000, 2000, 4000, 8000]);
  });

  it('never exceeds the cap', () => {
    const delays = [6, 7, 10, 50, 1000].map((attempt) => reconnectDelay(attempt, maxRandom));

    expect(delays.every((delay) => delay === DEFAULT_BACKOFF.maxMs)).toBe(true);
    expect(DEFAULT_BACKOFF.maxMs).toBe(30_000);
  });

  it('applies jitter below the exponential delay', () => {
    expect(reconnectDelay(3, minRandom)).toBe(4000 * (1 - DEFAULT_BACKOFF.jitterRatio));
    expect(reconnectDelay(3, () => 0.5)).toBe(4000 * (1 - DEFAULT_BACKOFF.jitterRatio / 2));
  });

  it('keeps the jittered delay positive at the cap', () => {
    expect(reconnectDelay(100, minRandom)).toBe(30_000 * (1 - DEFAULT_BACKOFF.jitterRatio));
  });

  it('treats negative or fractional attempts as the first attempt or its floor', () => {
    expect(reconnectDelay(-3, maxRandom)).toBe(500);
    expect(reconnectDelay(2.7, maxRandom)).toBe(2000);
  });
});
