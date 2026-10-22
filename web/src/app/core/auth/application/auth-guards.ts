import { inject } from '@angular/core';
import { type CanActivateFn, RedirectCommand, Router } from '@angular/router';
import { DeviceSession } from './device-session';
import { AdminTokenStore } from './token-stores';

/** The query parameter the agent's editor link carries the admin token in. */
export const ADMIN_TOKEN_PARAM = 'admin';

/**
 * Moves `?admin=<token>` into the admin token store and redirects to `/admin` without it, replacing the history entry,
 * so the token never stays in the address bar. Returns `null` when the URL carries no admin token.
 */
function captureAdminToken(token: string | null): RedirectCommand | null {
  if (!token) {
    return null;
  }
  inject(AdminTokenStore).write(token);
  const router = inject(Router);
  return new RedirectCommand(router.createUrlTree(['/admin']), { replaceUrl: true });
}

/** `/`: the editor link goes to `/admin`; a paired device goes to its deck; anything else to pairing. */
export const entryGuard: CanActivateFn = (route) => {
  const admin = captureAdminToken(route.queryParamMap.get(ADMIN_TOKEN_PARAM));
  if (admin) {
    return admin;
  }
  const router = inject(Router);
  return router.createUrlTree([inject(DeviceSession).paired() ? '/deck' : '/pair']);
};

/** `/deck` needs a device token; without one the device has to pair first. */
export const pairedGuard: CanActivateFn = () =>
  inject(DeviceSession).paired() || inject(Router).createUrlTree(['/pair']);

/** `/admin?admin=<token>`: keeps the token and strips it from the URL. */
export const adminTokenGuard: CanActivateFn = (route) =>
  captureAdminToken(route.queryParamMap.get(ADMIN_TOKEN_PARAM)) ?? true;
