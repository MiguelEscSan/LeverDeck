import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminConnectionFacade } from '../../../core/connection/application/connection.facade';
import { RequestError } from '../../../core/connection/domain/request-error';
import { customIconId } from '../../../core/keys/domain/key-icons';
import type { EventMessage, InstalledGameDto, RegisteredAppDto, SoundDto, StoreAppDto } from '../../../core/protocol';
import { soundIdFromUuid, soundNameError, soundUploadError } from '../../../core/sounds/domain/sound-clip';
import { cleanPath, storeAppPath } from '../domain/app-registration';
import { IconEncoder } from './icon-encoder.port';
import { IdSource } from './id-source.port';
import { SoundTransport } from './sound-transport.port';

/**
 * The editor's library: what keys can launch besides keystrokes (the apps registered on the PC, kept live by
 * `settingsUpdated`, and the games Steam and Epic report as installed, read when the editor connects and again on
 * request), the icons the owner uploaded (kept live by `iconsChanged`) and the sound clips (kept live by
 * `soundsChanged`; uploads and previews go over HTTP, see {@link SoundTransport}).
 */
@Injectable({ providedIn: 'root' })
export class LibraryFacade {
  private readonly connection = inject(AdminConnectionFacade);
  private readonly ids = inject(IdSource);
  private readonly encoder = inject(IconEncoder);
  private readonly transport = inject(SoundTransport);

  private readonly appsState = signal<RegisteredAppDto[]>([]);
  private readonly gamesState = signal<InstalledGameDto[]>([]);
  private readonly gamesLoadingState = signal(false);
  private readonly storeAppsState = signal<StoreAppDto[]>([]);
  private readonly busyState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly iconsState = signal<string[]>([]);
  private readonly uploadingState = signal(false);
  private readonly soundsState = signal<SoundDto[]>([]);
  private readonly soundUploadingState = signal(false);

  readonly apps = this.appsState.asReadonly();
  readonly games = this.gamesState.asReadonly();
  readonly gamesLoading = this.gamesLoadingState.asReadonly();
  readonly storeApps = this.storeAppsState.asReadonly();
  readonly busy = this.busyState.asReadonly();
  readonly error = this.errorState.asReadonly();
  /** Uploaded icon ids. */
  readonly icons = this.iconsState.asReadonly();
  readonly uploading = this.uploadingState.asReadonly();
  /** The uploaded sound clips, by name. */
  readonly sounds = this.soundsState.asReadonly();
  readonly soundUploading = this.soundUploadingState.asReadonly();

  constructor() {
    effect(() => {
      if (this.connection.connected()) {
        untracked(() => {
          void this.loadApps();
          void this.loadIcons();
          void this.loadSounds();
          void this.refreshGames();
          void this.refreshStoreApps();
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

  /** Reads the Microsoft Store apps in the Start menu; they are listed again each time the editor connects. */
  async refreshStoreApps(): Promise<void> {
    try {
      this.storeAppsState.set((await this.connection.query('getStoreApps', {})).apps);
    } catch {
      this.errorState.set("Couldn't read the Microsoft Store apps.");
    }
  }

  /** Registers a Store app by its AppUserModelId; resolves to its new id, or `null` when the agent refused it. */
  registerStoreApp(app: StoreAppDto): Promise<string | null> {
    return this.registerApp(app.name, storeAppPath(app.appUserModelId));
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

  /**
   * Uploads a WAV as a new clip named `name`, checked here first and then by the agent. Resolves to whether it was
   * stored (see {@link error} otherwise).
   */
  async uploadSound(file: File, name: string): Promise<boolean> {
    const problem = soundUploadError(file) ?? soundNameError(name);
    if (problem) {
      this.errorState.set(problem);
      return false;
    }
    if (this.soundUploadingState()) {
      return false;
    }
    this.soundUploadingState.set(true);
    this.errorState.set(null);
    try {
      await this.transport.upload(soundIdFromUuid(this.ids.next()), name.trim(), file);
      await this.loadSounds();
      return true;
    } catch (error) {
      this.errorState.set(error instanceof Error ? error.message : "Couldn't upload the sound. Try again.");
      return false;
    } finally {
      this.soundUploadingState.set(false);
    }
  }

  async renameSound(soundId: string, name: string): Promise<void> {
    const problem = soundNameError(name);
    if (problem) {
      this.errorState.set(problem);
      return;
    }
    this.errorState.set(null);
    try {
      await this.connection.command('renameSound', { soundId, name: name.trim() });
    } catch (error) {
      this.errorState.set(this.reason(error, "Couldn't rename the sound. Try again."));
    }
  }

  async deleteSound(soundId: string): Promise<void> {
    if (this.busyState()) {
      return;
    }
    this.busyState.set(true);
    this.errorState.set(null);
    try {
      await this.connection.command('deleteSound', { soundId });
    } catch (error) {
      this.errorState.set(
        error instanceof RequestError && error.code === 'validationFailed'
          ? 'A key still plays this sound. Change that key first.'
          : "Couldn't delete the sound. Try again.",
      );
    } finally {
      this.busyState.set(false);
    }
  }

  /** Plays a stored clip on this PC so the owner can hear it. */
  async previewSound(soundId: string): Promise<void> {
    this.errorState.set(null);
    try {
      await this.transport.preview(soundId);
    } catch (error) {
      this.errorState.set(error instanceof Error ? error.message : "Couldn't play the sound.");
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

  private async loadSounds(): Promise<void> {
    try {
      this.soundsState.set((await this.connection.query('getSounds', {})).sounds);
    } catch {
      this.errorState.set("Couldn't read the sound library.");
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
    } else if (event.type === 'soundsChanged') {
      this.soundsState.set(event.payload.sounds);
    }
  }

  private reason(error: unknown, fallback: string): string {
    return error instanceof RequestError && error.code === 'validationFailed' ? error.message : fallback;
  }
}
