import type { NetworkStatus } from './websocket-connection';

/** {@link NetworkStatus} backed by `navigator.onLine` and the window `online`/`offline` events. */
export function browserNetwork(window: Window): NetworkStatus {
  return {
    isOnline: () => window.navigator.onLine,
    watch: (onChange) => {
      const online = () => onChange(true);
      const offline = () => onChange(false);
      window.addEventListener('online', online);
      window.addEventListener('offline', offline);
      return () => {
        window.removeEventListener('online', online);
        window.removeEventListener('offline', offline);
      };
    },
  };
}
