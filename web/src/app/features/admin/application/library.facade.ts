import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import { RequestError } from '../../../core/connection/domain/request-error';
import type { EventMessage, InstalledGameDto, RegisteredAppDto } from '../../../core/protocol';
import { cleanPath } from '../domain/app-registration';

/**
 * What keys can launch besides keystrokes: the apps registered on the PC (kept live by `settingsUpdated`) and the
 * games Steam and Epic report as installed (read when the editor connects, and again on request).
 */
@Injectable({ providedIn: 'root' })
export class LibraryFacade {
  private readonly connection = inject(AdminConnectionFacade);

  private readonly appsState = signal<RegisteredAppDto[]>([]);
  private readonly gamesState = signal<InstalledGameDto[]>([]);
  private readonly gamesLoadingState = signal(false);
  private readonly busyState = signal(false);
  private readonly errorState = signal<string | null>(null);

  readonly apps = this.appsState.asReadonly();
  readonly games = this.gamesState.asReadonly();
  readonly gamesLoading = this.gamesLoadingState.asReadonly();
  readonly busy = this.busyState.asReadonly();
  readonly error = this.errorState.asReadonly();

  constructor() {
    effect(() => {
      if (this.connection.connected()) {
        untracked(() => {
          void this.loadApps();
          void this.refreshGames();
        });
      }
    });
    this.connection.events.pipe(takeUntilDestroyed()).subscribe((event) => this.onEvent(event));
  }

  async refreshGames(): Promise<void> {
    this.gamesLoadingState.set(true);
    try {
      this.gamesState.set((await this.connection.query('getInstalledGames', {})).games);
    } catch {
      this.errorState.set("Couldn't read the installed games.");
    } finally {
      this.gamesLoadingState.set(false);
    }
  }

  /** Registers an executable; resolves to its new id, or `null` when the agent refused it (see {@link error}). */
  async registerApp(displayName: string, path: string): Promise<string | null> {
    if (this.busyState()) {
      return null;
    }
    this.busyState.set(true);
    this.errorState.set(null);
    try {
      const ack = await this.connection.command('registerApp', { displayName: displayName.trim(), path: cleanPath(path) });
      // `settingsUpdated` normally arrived first; read the settings if it did not.
      const appId = ack.payload?.appId ?? null;
      if (appId && !this.appsState().some((app) => app.appId === appId)) {
        await this.loadApps();
      }
      return appId;
    } catch (error) {
      this.errorState.set(this.reason(error, "Couldn't register the app. Try again."));
      return null;
    } finally {
      this.busyState.set(false);
    }
  }

  async unregisterApp(appId: string): Promise<void> {
    if (this.busyState()) {
      return;
    }
    this.busyState.set(true);
    this.errorState.set(null);
    try {
      await this.connection.command('unregisterApp', { appId });
    } catch (error) {
      this.errorState.set(
        error instanceof RequestError && error.code === 'validationFailed'
          ? 'A key still launches this app. Change or remove that key first.'
          : "Couldn't remove the app. Try again.",
      );
    } finally {
      this.busyState.set(false);
    }
  }

  dismissError(): void {
    this.errorState.set(null);
  }

  private async loadApps(): Promise<void> {
    try {
      this.appsState.set((await this.connection.query('getSettings', {})).settings.apps);
    } catch {
      this.errorState.set("Couldn't read the registered apps.");
    }
  }

  private onEvent(event: EventMessage): void {
    if (event.type === 'settingsUpdated') {
      this.appsState.set(event.payload.settings.apps);
    }
  }

  private reason(error: unknown, fallback: string): string {
    return error instanceof RequestError && error.code === 'validationFailed' ? error.message : fallback;
  }
}
