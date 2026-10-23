import { TestBed } from '@angular/core/testing';
import type { ThemePreference } from '../domain/theme';
import { ThemeFacade } from './theme.facade';
import { ThemeStore, ThemeTarget } from './theme.ports';

class FakeThemeStore extends ThemeStore {
  writes: ThemePreference[] = [];

  constructor(private stored: ThemePreference) {
    super();
  }

  read(): ThemePreference {
    return this.stored;
  }

  write(preference: ThemePreference): void {
    this.stored = preference;
    this.writes.push(preference);
  }
}

class FakeThemeTarget extends ThemeTarget {
  applied: ThemePreference[] = [];

  apply(preference: ThemePreference): void {
    this.applied.push(preference);
  }
}

const setUp = (stored: ThemePreference = 'system') => {
  const store = new FakeThemeStore(stored);
  const target = new FakeThemeTarget();
  TestBed.configureTestingModule({
    providers: [
      { provide: ThemeStore, useValue: store },
      { provide: ThemeTarget, useValue: target },
    ],
  });
  return { facade: TestBed.inject(ThemeFacade), store, target };
};

describe('ThemeFacade', () => {
  it('starts from the stored preference and applies it', () => {
    const { facade, target } = setUp('light');

    expect(facade.preference()).toBe('light');
    expect(target.applied).toEqual(['light']);
  });

  it('applies and remembers a new choice', () => {
    const { facade, store, target } = setUp();

    facade.choose('dark');

    expect(facade.preference()).toBe('dark');
    expect(store.writes).toEqual(['dark']);
    expect(target.applied).toEqual(['system', 'dark']);
  });

  it('ignores the choice already in place', () => {
    const { facade, store, target } = setUp('dark');

    facade.choose('dark');

    expect(store.writes).toEqual([]);
    expect(target.applied).toEqual(['dark']);
  });
});
