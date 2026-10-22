import { KEY_ICONS, keyIconPath, keyMonogram } from './key-icons';

describe('KEY_ICONS', () => {
  it('has unique kebab-case ids, as the agent requires', () => {
    const ids = KEY_ICONS.map((icon) => icon.id);

    expect(new Set(ids).size).toBe(ids.length);
    expect(ids.every((id) => /^[a-z0-9]+(-[a-z0-9]+)*$/.test(id) && id.length <= 64)).toBe(true);
  });

  it('covers the icons of the seeded default profile', () => {
    for (const id of ['play-pause', 'skip-next', 'skip-previous', 'volume-up', 'volume-down', 'volume-mute']) {
      expect(keyIconPath(id)).not.toBeNull();
    }
  });
});

describe('keyIconPath', () => {
  it('gives null for an id outside the set', () => {
    expect(keyIconPath('unknown-icon')).toBeNull();
  });
});

describe('keyMonogram', () => {
  it('uses the label first, then the icon id', () => {
    expect(keyMonogram('OBS scene', 'x')).toBe('OB');
    expect(keyMonogram('  ', 'my-icon')).toBe('MY');
    expect(keyMonogram('', '--')).toBe('•');
  });
});
