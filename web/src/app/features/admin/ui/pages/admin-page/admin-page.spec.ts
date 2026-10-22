import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ConnectionStatus } from '../../../../../core/connection/domain/connection-status';
import type { ButtonDto, DeviceDto, PairingSessionDto, ProfileDto } from '../../../../../core/protocol';
import { type AdminAccess, AdminFacade } from '../../../application/admin.facade';
import { type EditorCell, type EditorSelection, ProfileEditorFacade } from '../../../application/profile-editor.facade';
import { AdminPage } from './admin-page';

const device: DeviceDto = {
  id: 'd1',
  name: 'Pixel 8',
  pairedAt: '2026-10-20T10:00:00.000Z',
  lastSeenAt: '2026-10-22T12:00:00.000Z',
  connected: true,
  revoked: false,
};

class FakeAdmin {
  readonly access = signal<AdminAccess>('ready');
  readonly status = signal<ConnectionStatus>('connected');
  readonly session = signal<PairingSessionDto | null>(null);
  readonly devices = signal<DeviceDto[]>([device]);
  readonly now = signal(Date.parse('2026-10-22T12:00:00.000Z'));
  readonly secondsLeft = signal(0);
  readonly expired = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly calls: string[] = [];

  start(): void {
    this.calls.push('start');
  }

  async startPairing(): Promise<void> {
    this.calls.push('startPairing');
  }

  async cancelPairing(): Promise<void> {
    this.calls.push('cancelPairing');
  }

  async revoke(deviceId: string): Promise<void> {
    this.calls.push(`revoke ${deviceId}`);
  }
}

const play: ButtonDto = {
  id: 'b1',
  row: 0,
  column: 0,
  label: 'Play',
  color: 'blue',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
};

const profile: ProfileDto = {
  id: 'p1',
  name: 'Streaming',
  color: 'red',
  grid: { columns: 3, rows: 3 },
  pages: [{ index: 0, buttons: [play] }],
};

class FakeEditor {
  readonly profiles = signal([{ id: 'p1', name: 'Streaming', color: 'red' as const }]);
  readonly profile = signal<ProfileDto | null>(profile);
  readonly page = signal(0);
  readonly pageCount = signal(1);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly canDeleteProfile = signal(false);
  readonly selection = signal<EditorSelection | null>(null);
  readonly cells = signal<EditorCell[]>(
    Array.from({ length: 9 }, (_, i) => ({
      row: Math.floor(i / 3),
      column: i % 3,
      button: i === 0 ? play : null,
    })),
  );
  readonly calls: string[] = [];

  selectCell(row: number, column: number): void {
    this.calls.push(`select ${row}:${column}`);
    this.selection.set(row === 0 && column === 0 ? { kind: 'key', button: play } : { kind: 'empty', row, column });
  }

  addKey(): void {
    this.calls.push('addKey');
  }

  updateKey(change: object): void {
    this.calls.push(`update ${JSON.stringify(change)}`);
  }

  createProfile(): void {
    this.calls.push('createProfile');
  }

  dismissError(): void {
    this.error.set(null);
  }
}

describe('AdminPage', () => {
  let admin: FakeAdmin;
  let editor: FakeEditor;

  beforeEach(() => {
    admin = new FakeAdmin();
    editor = new FakeEditor();
    TestBed.configureTestingModule({
      providers: [
        { provide: AdminFacade, useValue: admin },
        { provide: ProfileEditorFacade, useValue: editor },
      ],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(AdminPage);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('connects on open and shows pairing and the devices once ready', async () => {
    const { element } = await render();

    expect(admin.calls).toEqual(['start']);
    expect(element.querySelector('app-pairing-card')).not.toBeNull();
    expect(element.querySelector('app-device-list')?.textContent).toContain('Pixel 8');
    expect(element.querySelector('.status')?.textContent?.trim()).toBe('Agent connected');
  });

  it('wires the pairing and revoke buttons to the facade', async () => {
    const { fixture, element } = await render();
    const button = (label: string) =>
      [...element.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === label);

    button('Start pairing')?.click();
    button('Revoke')?.click();
    await fixture.whenStable();
    button('Revoke?')?.click();

    expect(admin.calls).toEqual(['start', 'startPairing', 'revoke d1']);
  });

  it('asks for the editor link when the tab has no admin token', async () => {
    admin.access.set('missingToken');
    const { element } = await render();

    expect(element.textContent).toContain('Open the editor from its link');
    expect(element.querySelector('app-pairing-card')).toBeNull();
    expect(element.querySelector('.status')).toBeNull();
  });

  it('explains a rejected editor link', async () => {
    admin.access.set('rejected');
    const { element } = await render();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('This editor link is not valid');
  });

  it('shows the open profile on the grid and edits the chosen key', async () => {
    const { fixture, element } = await render();

    expect(element.querySelector('app-profile-list')?.textContent).toContain('Streaming');
    expect((element.querySelector('#profile-name') as HTMLInputElement).value).toBe('Streaming');
    const cells = element.querySelectorAll('app-editor-grid .cell');
    expect(cells).toHaveLength(9);

    (cells[0] as HTMLButtonElement).click();
    await fixture.whenStable();
    const label = element.querySelector('#key-label') as HTMLInputElement;
    expect(label.value).toBe('Play');
    label.value = 'Pause';
    label.dispatchEvent(new Event('change'));

    expect(editor.calls).toEqual(['select 0:0', 'update {"label":"Pause"}']);
  });

  it('adds a key on a chosen empty position', async () => {
    const { fixture, element } = await render();

    (element.querySelectorAll('app-editor-grid .cell')[4] as HTMLButtonElement).click();
    await fixture.whenStable();
    [...element.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Add a key here')?.click();

    expect(editor.calls).toEqual(['select 1:1', 'addKey']);
  });

  it('shows and dismisses an editor error', async () => {
    editor.error.set('One key is outside 3 × 3. Move or remove it first.');
    const { fixture, element } = await render();

    expect(element.querySelector('.banner')?.textContent).toContain('One key is outside 3 × 3.');
    (element.querySelector('.dismiss') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(element.querySelector('.banner')).toBeNull();
  });

  it('shows the facade error above the cards', async () => {
    admin.error.set("Couldn't start pairing. Try again.");
    const { element } = await render();

    expect(element.querySelector('.error')?.textContent).toBe("Couldn't start pairing. Try again.");
  });
});
