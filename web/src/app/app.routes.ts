import type { Routes } from '@angular/router';
import { adminTokenGuard, entryGuard, pairedGuard } from './core/auth/application/auth-guards';
import { AdminPage } from './features/admin/ui/pages/admin-page/admin-page';
import { DeckPage } from './features/deck/ui/pages/deck-page/deck-page';
import { PairingPage } from './features/pairing/ui/pages/pairing-page/pairing-page';
import { ShellPage } from './features/shell/ui/pages/shell-page/shell-page';

/**
 * `/` only routes: the editor link (`/?admin=…`) to `/admin`, a paired device to `/deck`, anything else to `/pair`.
 * The deck lives inside the shell (header with the connection badge); pairing and the editor have their own layout.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', canActivate: [entryGuard], children: [] },
  { path: 'pair', component: PairingPage, title: 'Pair · LeverDeck' },
  {
    path: 'deck',
    component: ShellPage,
    canActivate: [pairedGuard],
    children: [{ path: '', component: DeckPage, title: 'LeverDeck' }],
  },
  { path: 'admin', component: AdminPage, canActivate: [adminTokenGuard], title: 'Editor · LeverDeck' },
  { path: '**', redirectTo: '' },
];
