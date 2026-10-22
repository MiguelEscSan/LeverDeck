/** A bundled key icon: an id (lowercase kebab-case, as the agent stores it), a name for pickers and a 24×24 path. */
export interface KeyIcon {
  readonly id: string;
  readonly name: string;
  /** Filled with the key's icon color, `evenodd`, so inner shapes cut holes. */
  readonly path: string;
}

/**
 * The bundled icon set, shared by the deck and the editor. The first six are the ones the agent seeds in the default
 * profile. Any other stored id still renders, as a monogram.
 */
export const KEY_ICONS: readonly KeyIcon[] = [
  { id: 'play-pause', name: 'Play/Pause', path: 'M4 5v14l9-7zM15 5h2.5v14H15zM19.5 5H22v14h-2.5z' },
  { id: 'skip-next', name: 'Next track', path: 'M5 5v14l10-7zM16 5h3v14h-3z' },
  { id: 'skip-previous', name: 'Previous track', path: 'M19 5v14L9 12zM5 5h3v14H5z' },
  {
    id: 'volume-up',
    name: 'Volume up',
    path: 'M3 9v6h4l5 5V4L7 9zM14.5 7.5a6 6 0 0 1 0 9l-1.4-1.4a4 4 0 0 0 0-6.2zM17.3 4.7a10 10 0 0 1 0 14.6l-1.4-1.4a8 8 0 0 0 0-11.8z',
  },
  {
    id: 'volume-down',
    name: 'Volume down',
    path: 'M5 9v6h4l5 5V4L9 9zM16.5 7.5a6 6 0 0 1 0 9l-1.4-1.4a4 4 0 0 0 0-6.2z',
  },
  {
    id: 'volume-mute',
    name: 'Mute',
    path: 'M3 9v6h4l5 5V4L7 9zM15 9.4l1.4-1.4 2.1 2.1 2.1-2.1 1.4 1.4-2.1 2.1 2.1 2.1-1.4 1.4-2.1-2.1-2.1 2.1-1.4-1.4 2.1-2.1z',
  },
  { id: 'stop', name: 'Stop', path: 'M6 6h12v12H6z' },
  {
    id: 'microphone',
    name: 'Microphone',
    path: 'M12 2a3 3 0 0 1 3 3v6a3 3 0 0 1-6 0V5a3 3 0 0 1 3-3zM5 10h2a5 5 0 0 0 10 0h2a7 7 0 0 1-6 6.9V21h-2v-4.1A7 7 0 0 1 5 10z',
  },
  {
    id: 'microphone-off',
    name: 'Microphone off',
    path: 'M12 2a3 3 0 0 1 3 3v6a3 3 0 0 1-6 0V5a3 3 0 0 1 3-3zM5 10h2a5 5 0 0 0 10 0h2a7 7 0 0 1-6 6.9V21h-2v-4.1A7 7 0 0 1 5 10zM2.3 3.7l1.4-1.4 18 18-1.4 1.4z',
  },
  {
    id: 'camera',
    name: 'Camera',
    path: 'M3 6h12a1 1 0 0 1 1 1v3l5-3v10l-5-3v3a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1z',
  },
  { id: 'record', name: 'Record', path: 'M12 4a8 8 0 1 1 0 16 8 8 0 0 1 0-16z' },
  {
    id: 'broadcast',
    name: 'Go live',
    path: 'M12 10a2 2 0 1 1 0 4 2 2 0 0 1 0-4zM7.8 7.8l1.4 1.4a4 4 0 0 0 0 5.6l-1.4 1.4a6 6 0 0 1 0-8.4zM16.2 7.8a6 6 0 0 1 0 8.4l-1.4-1.4a4 4 0 0 0 0-5.6zM5 5l1.4 1.4a8 8 0 0 0 0 11.2L5 19a10 10 0 0 1 0-14zM19 5a10 10 0 0 1 0 14l-1.4-1.4a8 8 0 0 0 0-11.2z',
  },
  { id: 'scene', name: 'Scene', path: 'M3 7h7l2 2h9v10H3z' },
  {
    id: 'screenshot',
    name: 'Screenshot',
    path: 'M4 4h5v2H6v3H4zM15 4h5v5h-2V6h-3zM4 15h2v3h3v2H4zM18 15h2v5h-5v-2h3zM12 9a3 3 0 1 1 0 6 3 3 0 0 1 0-6z',
  },
  { id: 'lightning', name: 'Lightning', path: 'M13 2 4 14h6l-1 8 9-12h-6z' },
  { id: 'star', name: 'Star', path: 'M12 2l3 6.5 7 .8-5.2 4.8 1.5 6.9L12 17.6 5.7 21l1.5-6.9L2 9.3l7-.8z' },
  {
    id: 'keyboard',
    name: 'Keyboard',
    path: 'M2 6h20v12H2zM5 9v2h2V9zM9 9v2h2V9zM13 9v2h2V9zM17 9v2h2V9zM7 13v2h10v-2z',
  },
  {
    id: 'gamepad',
    name: 'Gamepad',
    path: 'M7 7h10a5 5 0 0 1 0 10c-1.5 0-2.5-1-3.5-2h-3c-1 1-2 2-3.5 2A5 5 0 0 1 7 7zM6 10v1.5H4.5v2H6V15h2v-1.5h1.5v-2H8V10zM16 10.5a1.2 1.2 0 1 0 0 .1zM18 13a1.2 1.2 0 1 0 0 .1z',
  },
  { id: 'page-next', name: 'Next page', path: 'M9 5l7 7-7 7-1.4-1.4 5.6-5.6-5.6-5.6z' },
  { id: 'page-previous', name: 'Previous page', path: 'M15 5l-7 7 7 7 1.4-1.4-5.6-5.6 5.6-5.6z' },
  {
    id: 'globe',
    name: 'Web page',
    path: 'M12 2a10 10 0 1 1 0 20 10 10 0 0 1 0-20zM4.1 11h3.4c.1-2.4.6-4.6 1.4-6.2A8 8 0 0 0 4.1 11zM9.5 11h5c-.2-3.2-1.2-5.6-2.5-6.6-1.3 1-2.3 3.4-2.5 6.6zM16.5 11h3.4a8 8 0 0 0-4.8-6.2c.8 1.6 1.3 3.8 1.4 6.2zM4.1 13a8 8 0 0 0 4.8 6.2c-.8-1.6-1.3-3.8-1.4-6.2zM9.5 13c.2 3.2 1.2 5.6 2.5 6.6 1.3-1 2.3-3.4 2.5-6.6zM16.5 13c-.1 2.4-.6 4.6-1.4 6.2a8 8 0 0 0 4.8-6.2z',
  },
  {
    id: 'window',
    name: 'App',
    path: 'M3 4h18a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1zM4 8v10h16V8z',
  },
  { id: 'layers', name: 'Profile', path: 'M12 3l10 5-10 5L2 8zM4.2 11.9 12 15.8l7.8-3.9L22 13l-10 5-10-5z' },
];

const PATHS: ReadonlyMap<string, string> = new Map(KEY_ICONS.map((icon) => [icon.id, icon.path]));

/** The path of a bundled icon, or `null` for an id outside the set. */
export const keyIconPath = (iconId: string): string | null => PATHS.get(iconId) ?? null;

/** The placeholder for an unknown icon: the first two letters or digits of the label, else of the icon id. */
export function keyMonogram(label: string, iconId: string): string {
  const source = (label.trim() || iconId).replace(/[^\p{L}\p{N}]/gu, '');
  return source.slice(0, 2).toUpperCase() || '•';
}

/** Uploaded icons are `custom-` plus 32 lowercase hex digits, as the agent requires. */
const CUSTOM_ICON = /^custom-[0-9a-f]{32}$/;

export const isCustomIconId = (iconId: string): boolean => CUSTOM_ICON.test(iconId);

/** Where the agent serves an uploaded icon (same origin, cached forever: an id's image never changes). */
export const customIconUrl = (iconId: string): string => `/api/icons/${iconId}.png`;

/** A new uploaded-icon id from a UUID. */
export const customIconId = (uuid: string): string => `custom-${uuid.replace(/-/g, '').toLowerCase()}`;
