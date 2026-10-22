import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AdminFacade } from '../../../application/admin.facade';
import { DeviceList } from '../../components/device-list/device-list';
import { PairingCard } from '../../components/pairing-card/pairing-card';

const STATUS_LABELS = {
  connected: 'Agent connected',
  reconnecting: 'Reconnecting…',
  offline: 'Offline',
} as const;

/**
 * `/admin`: the desktop editor, opened from the link the agent logs at start-up (docs/mocks/desktop.html). Phase 6
 * shows pairing and the paired devices; profiles and the grid editor arrive in Phase 7.
 */
@Component({
  selector: 'app-admin-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DeviceList, PairingCard],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss',
})
export class AdminPage {
  protected readonly admin = inject(AdminFacade);

  protected readonly statusLabel = computed(() => STATUS_LABELS[this.admin.status()]);

  constructor() {
    this.admin.start();
  }
}
