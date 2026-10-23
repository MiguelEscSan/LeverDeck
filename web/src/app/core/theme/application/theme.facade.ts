import { Injectable, inject, signal } from '@angular/core';
import { THEME_PREFERENCES, type ThemePreference } from '../domain/theme';
import { ThemeStore, ThemeTarget } from './theme.ports';

/** The theme of this device, shared by the deck and the editor. Per device: it is never sent to the agent. */
@Injectable({ providedIn: 'root' })
export class ThemeFacade {
  private readonly store = inject(ThemeStore);
  private readonly target = inject(ThemeTarget);
  private readonly current = signal(this.store.read());

  readonly preference = this.current.asReadonly();
  readonly options = THEME_PREFERENCES;

  constructor() {
    // index.html already applied it before the first paint; this keeps the document in step with the store.
    this.target.apply(this.current());
  }

  choose(preference: ThemePreference): void {
    if (preference === this.current()) {
      return;
    }
    this.current.set(preference);
    this.store.write(preference);
    this.target.apply(preference);
  }
}
