import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import { RequestError } from '../../../core/connection/domain/request-error';
import { customIconId } from '../../../core/keys/domain/key-icons';
import type { EventMessage, InstalledGameDto, RegisteredAppDto } from '../../../core/protocol';
import { cleanPath } from '../domain/app-registration';
import { IconEncoder } from './icon-encoder.port';
import { IdSource } from './id-source.port';

/**
 * The editor's library: what keys can launch besides keystrokes (the apps registered on the PC, kept live by
 * `settingsUpdated`, and the games Steam and Epic report as installed, read when the editor connects and again on
 * request) and the icons the owner uploaded (kept live by `iconsChanged`).
 */
@Injectable({ providedIn: 'root' })
export class LibraryFacade {
  private readonly connection = inject(AdminConnectionFacade);
  private readonly ids = inject(IdSource);
  private readonly encoder = inject(IconEncoder);

  private readonly appsState = signal<RegisteredAppDto[]>([]);
  private readonly gamesState = signal<InstalledGameDto[]>([]);
  private readonly gamesLoadingState = signal(false);
  private readonly busyState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly iconsState = signal<string[]>([]);
  private readonly uploadingState = signal(false);

  readonly apps = this.appsState.asReadonly();
  readonly games = this.gamesState.asReadonly();
  readonly gamesLoading = this.gamesLoadingState.asReadonly();
  readonly busy = this.busyState.asReadonly();
  readonly error = this.errorState.asReadonly();
  /** Uploaded icon ids. */
  readonly icons = this.iconsState.asReadonly();
  readonly uploading = this.uploadingState.asReadonly();

  constructor() {
    effect(() => {
      if (this.connection.connected()) {
        untracked(() => {
          void this.loadApps();
          void this.loadIcons();
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

  /**
   * Turns a picture into a 256 × 256 PNG icon and uploads it; resolves to the new icon id, or `null` when the picture
   * could not be read or the agent refused it (see {@link error}).
   */
  async uploadIcon(file: Blob): Promise<string | null> {
    if (this.uploadingState()) {
      return null;
    }
    this.uploadingState.set(true);
    this.errorState.set(null);
    try {
      let data: string;
      try {
        data = await this.encoder.encode(file);
      } catch (error) {
        this.errorState.set(error instanceof Error ? error.message : "Couldn't read that picture.");
        return null;
      }
      const iconId = customIconId(this.ids.next());
      await this.connection.command('saveIcon', { iconId, data });
      if (!this.iconsState().includes(iconId)) {
        this.iconsState.update((icons) => [...icons, iconId]);
      }
      return iconId;
    } catch (error) {
      this.errorState.set(this.reason(error, "Couldn't upload the icon. Try again."));
      return null;
    } finally {
      this.uploadingState.set(false);
    }
  }

  async deleteIcon(iconId: string): Promise<void> {
    if (this.busyState()) {
      return;
    }
    this.busyState.set(true);
    this.errorState.set(null);
    try {
      await this.connection.command('deleteIcon', { iconId });
    } catch (error) {
      this.errorState.set(
        error instanceof RequestError && error.code === 'validationFailed'
          ? 'A key still shows this icon. Give that key another icon first.'
          : "Couldn't delete the icon. Try again.",
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

  private async loadIcons(): Promise<void> {
    try {
      this.iconsState.set((await this.connection.query('getIcons', {})).icons);
    } catch {
      this.errorState.set("Couldn't read the uploaded icons.");
    }
  }

  private onEvent(event: EventMessage): void {
    if (event.type === 'settingsUpdated') {
      this.appsState.set(event.payload.settings.apps);
    } else if (event.type === 'iconsChanged') {
      this.iconsState.set(event.payload.icons);
    }
  }

  private reason(error: unknown, fallback: string): string {
    return error instanceof RequestError && error.code === 'validationFailed' ? error.message : fallback;
  }
}
