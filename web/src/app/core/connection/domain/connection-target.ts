/**
 * Who a connection speaks for (docs/ARCHITECTURE.md, Transport endpoints):
 *
 * - `device`: `/ws`, authenticated by sending `connectDevice` with the device token as the first message.
 * - `admin`: `/admin/ws?token=…`, authenticated by the agent before the upgrade.
 */
export type ConnectionTarget =
  | { readonly scope: 'device'; readonly token: string }
  | { readonly scope: 'admin'; readonly token: string };

export const sameTarget = (a: ConnectionTarget | null, b: ConnectionTarget | null): boolean =>
  a !== null && b !== null && a.scope === b.scope && a.token === b.token;
