/**
 * Builds request ids that are unique per page: a random session prefix plus a counter. `crypto.randomUUID()` is not
 * used because it needs a secure context, and the PWA runs on plain `http://` on the LAN.
 */
export function requestIds(random: () => number = Math.random): () => string {
  const session = Math.floor(random() * 2 ** 32)
    .toString(36)
    .padStart(7, '0');
  let counter = 0;
  return () => `${session}-${(counter++).toString(36)}`;
}
