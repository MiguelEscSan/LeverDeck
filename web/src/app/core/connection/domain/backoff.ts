/** Exponential backoff with jitter for reconnection attempts. */
export interface BackoffPolicy {
  /** Delay before the first retry. */
  readonly baseMs: number;
  /** Upper bound for any delay. */
  readonly maxMs: number;
  /** Growth per attempt. */
  readonly factor: number;
  /** Share of the delay (0–1) that is randomized, so many devices do not retry in lockstep. */
  readonly jitterRatio: number;
}

export const DEFAULT_BACKOFF: BackoffPolicy = {
  baseMs: 500,
  maxMs: 30_000,
  factor: 2,
  jitterRatio: 0.2,
};

/**
 * Delay before retry number `attempt` (0-based): `min(max, base × factor^attempt)`, reduced by up to `jitterRatio`
 * depending on `random()` (a value in [0, 1]). The result never exceeds `maxMs`.
 */
export function reconnectDelay(
  attempt: number,
  random: () => number,
  policy: BackoffPolicy = DEFAULT_BACKOFF,
): number {
  const step = Math.max(0, Math.floor(attempt));
  const exponential = Math.min(policy.maxMs, policy.baseMs * policy.factor ** step);
  const spread = Math.min(1, Math.max(0, random()));
  return exponential * (1 - policy.jitterRatio * (1 - spread));
}
