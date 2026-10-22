import { requestIds } from './request-ids';

describe('requestIds', () => {
  it('never repeats an id within a page', () => {
    const next = requestIds(() => 0.5);
    const ids = Array.from({ length: 1000 }, next);

    expect(new Set(ids).size).toBe(1000);
  });

  it('prefixes ids per page, so two pages do not collide', () => {
    expect(requestIds(() => 0.1)()).not.toBe(requestIds(() => 0.9)());
  });
});
