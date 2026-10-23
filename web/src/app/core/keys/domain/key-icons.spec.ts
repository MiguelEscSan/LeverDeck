import {
  customIconId,
  customIconUrl,
  hasKeyIcon,
  isCustomIconId,
  KEY_ICONS,
  keyIconPath,
  keyMonogram,
} from './key-icons';

describe('hasKeyIcon', () => {
  it('is true for a bundled or uploaded icon, false for a placeholder id', () => {
    expect(hasKeyIcon('play-pause')).toBe(true);
    expect(hasKeyIcon(customIconId('0123456789abcdef0123456789abcdef'))).toBe(true);
    expect(hasKeyIcon('obs-scene')).toBe(false);
    expect(hasKeyIcon('')).toBe(false);
  });
});

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

describe('custom icons', () => {
  it('are named from a UUID and served by the agent', () => {
    const id = customIconId('0123ABCD-89ab-4def-8123-456789abcdef');

    expect(id).toBe('custom-0123abcd89ab4def8123456789abcdef');
    expect(isCustomIconId(id)).toBe(true);
    expect(customIconUrl(id)).toBe('/api/icons/custom-0123abcd89ab4def8123456789abcdef.png');
  });

  it('are told apart from bundled and malformed ids', () => {
    expect(isCustomIconId('play-pause')).toBe(false);
    expect(isCustomIconId('custom-123')).toBe(false);
    expect(isCustomIconId('custom-0123abcd89ab4def8123456789abcdeg')).toBe(false);
  });
});
