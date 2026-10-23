import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ThemeFacade } from '../../../../../core/theme/application/theme.facade';
import { AdminFacade } from '../../../application/admin.facade';
import { LibraryFacade } from '../../../application/library.facade';
import { ProfileEditorFacade } from '../../../application/profile-editor.facade';
import { gridLabel } from '../../../domain/profile-editing';
import { AppList, type AppRegistration } from '../../components/app-list/app-list';
import { DeviceList } from '../../components/device-list/device-list';
import { EditorGrid } from '../../components/editor-grid/editor-grid';
import { IconGallery } from '../../components/icon-gallery/icon-gallery';
import { KeyInspector } from '../../components/key-inspector/key-inspector';
import { LibraryNav, type LibrarySection } from '../../components/library-nav/library-nav';
import { PageBar } from '../../components/page-bar/page-bar';
import { PairingCard } from '../../components/pairing-card/pairing-card';
import { ProfileList } from '../../components/profile-list/profile-list';
import { ProfileToolbar } from '../../components/profile-toolbar/profile-toolbar';
import { SoundLibrary } from '../../components/sound-library/sound-library';
import { ThemeSwitch } from '../../components/theme-switch/theme-switch';

/** What the middle column shows: the open profile's keys, or one of the library sections. */
export type EditorView = 'keys' | LibrarySection;

const STATUS_LABELS = {
  connected: 'Agent connected',
  reconnecting: 'Reconnecting…',
  offline: 'Offline',
} as const;

/**
 * `/admin`: the desktop editor, opened from the link the agent logs at start-up (docs/mocks/desktop.html). The PC is
 * where layouts change: profiles on the left, the page's grid in the middle, the selected key, pairing and the paired
 * devices on the right. Phones only show what is saved here.
 */
@Component({
  selector: 'app-admin-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgTemplateOutlet,
    AppList,
    DeviceList,
    EditorGrid,
    IconGallery,
    KeyInspector,
    LibraryNav,
    PageBar,
    PairingCard,
    ProfileList,
    ProfileToolbar,
    SoundLibrary,
    ThemeSwitch,
  ],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss',
})
export class AdminPage {
  protected readonly admin = inject(AdminFacade);
  protected readonly editor = inject(ProfileEditorFacade);
  protected readonly library = inject(LibraryFacade);
  protected readonly theme = inject(ThemeFacade);

  protected readonly view = signal<EditorView>('keys');
  protected readonly librarySection = computed(() => {
    const view = this.view();
    return view === 'keys' ? null : view;
  });
  protected readonly statusLabel = computed(() => STATUS_LABELS[this.admin.status()]);
  protected readonly selectedKey = computed(() => {
    const selection = this.editor.selection();
    return selection?.kind === 'key' ? selection.button : null;
  });
  protected readonly selectedEmpty = computed(() => {
    const selection = this.editor.selection();
    return selection?.kind === 'empty' ? { row: selection.row, column: selection.column } : null;
  });
  protected readonly selectedPosition = computed(() => {
    const selection = this.editor.selection();
    if (!selection) {
      return null;
    }
    return selection.kind === 'key'
      ? { row: selection.button.row, column: selection.button.column }
      : { row: selection.row, column: selection.column };
  });
  protected readonly gridName = computed(() => {
    const profile = this.editor.profile();
    return profile ? `${profile.name}, page ${this.editor.page() + 1}, ${gridLabel(profile.grid)}` : 'Keys';
  });

  constructor() {
    this.admin.start();
  }

  protected openProfile(profileId: string): void {
    this.view.set('keys');
    void this.editor.selectProfile(profileId);
  }

  protected createProfile(): void {
    this.view.set('keys');
    this.editor.createProfile();
  }

  /** Uploads a picture and puts it on the selected key. */
  protected async uploadKeyIcon(file: File): Promise<void> {
    const iconId = await this.library.uploadIcon(file);
    if (iconId) {
      this.editor.updateKey({ iconId });
    }
  }

  protected async uploadIcon(file: File): Promise<void> {
    await this.library.uploadIcon(file);
  }

  protected async uploadSound(upload: { readonly file: File; readonly name: string }): Promise<void> {
    await this.library.uploadSound(upload.file, upload.name);
  }

  protected async registerApp(registration: AppRegistration, form: AppList): Promise<void> {
    if (await this.library.registerApp(registration.displayName, registration.path)) {
      form.reset();
    }
  }
}
