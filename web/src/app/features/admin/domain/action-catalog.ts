import type {
  ActionDto,
  KeyModifierDto,
  MediaKeyDto,
  PageTargetDto,
  ProfileSummaryDto,
  VirtualKeyDto,
} from '../../../core/protocol';

/**
 * The actions the editor can configure for now. The others (launch app, open URL, OBS scene, launch game) need their
 * own pickers and arrive later in Phase 7; a key that already has one keeps it and shows it read-only.
 */
export type EditableActionType = 'hotkey' | 'mediaKey' | 'changePage' | 'switchProfile';

export const EDITABLE_ACTION_TYPES: readonly EditableActionType[] = ['hotkey', 'mediaKey', 'changePage', 'switchProfile'];

/** Every action type by name. A `Record` over the generated union, so a new action breaks the build until named. */
export const ACTION_TYPE_NAMES: Readonly<Record<ActionDto['type'], string>> = {
  hotkey: 'Hotkey',
  mediaKey: 'Media key',
  launchApp: 'Launch app',
  openUrl: 'Open URL',
  obsScene: 'OBS scene',
  launchGame: 'Launch game',
  changePage: 'Change page',
  switchProfile: 'Switch profile',
};

export const isEditableAction = (action: ActionDto): action is Extract<ActionDto, { type: EditableActionType }> =>
  (EDITABLE_ACTION_TYPES as readonly string[]).includes(action.type);

export const MEDIA_KEY_NAMES: Readonly<Record<MediaKeyDto, string>> = {
  playPause: 'Play/Pause',
  next: 'Next track',
  previous: 'Previous track',
  stop: 'Stop',
  volumeUp: 'Volume up',
  volumeDown: 'Volume down',
  mute: 'Mute',
};

export const MEDIA_KEYS = Object.keys(MEDIA_KEY_NAMES) as MediaKeyDto[];

/** Modifiers in the order they are shown and sent. */
export const MODIFIER_NAMES: Readonly<Record<KeyModifierDto, string>> = {
  ctrl: 'Ctrl',
  shift: 'Shift',
  alt: 'Alt',
  win: 'Win',
};

export const MODIFIERS = Object.keys(MODIFIER_NAMES) as KeyModifierDto[];

export const VIRTUAL_KEY_NAMES: Readonly<Record<VirtualKeyDto, string>> = {
  a: 'A',
  b: 'B',
  c: 'C',
  d: 'D',
  e: 'E',
  f: 'F',
  g: 'G',
  h: 'H',
  i: 'I',
  j: 'J',
  k: 'K',
  l: 'L',
  m: 'M',
  n: 'N',
  o: 'O',
  p: 'P',
  q: 'Q',
  r: 'R',
  s: 'S',
  t: 'T',
  u: 'U',
  v: 'V',
  w: 'W',
  x: 'X',
  y: 'Y',
  z: 'Z',
  d0: '0',
  d1: '1',
  d2: '2',
  d3: '3',
  d4: '4',
  d5: '5',
  d6: '6',
  d7: '7',
  d8: '8',
  d9: '9',
  f1: 'F1',
  f2: 'F2',
  f3: 'F3',
  f4: 'F4',
  f5: 'F5',
  f6: 'F6',
  f7: 'F7',
  f8: 'F8',
  f9: 'F9',
  f10: 'F10',
  f11: 'F11',
  f12: 'F12',
  f13: 'F13',
  f14: 'F14',
  f15: 'F15',
  f16: 'F16',
  f17: 'F17',
  f18: 'F18',
  f19: 'F19',
  f20: 'F20',
  f21: 'F21',
  f22: 'F22',
  f23: 'F23',
  f24: 'F24',
  up: 'Up arrow',
  down: 'Down arrow',
  left: 'Left arrow',
  right: 'Right arrow',
  enter: 'Enter',
  escape: 'Esc',
  space: 'Space',
  tab: 'Tab',
  backspace: 'Backspace',
  delete: 'Delete',
  insert: 'Insert',
  home: 'Home',
  end: 'End',
  pageUp: 'Page Up',
  pageDown: 'Page Down',
  printScreen: 'Print Screen',
};

export const VIRTUAL_KEYS = Object.keys(VIRTUAL_KEY_NAMES) as VirtualKeyDto[];

export const PAGE_TARGET_NAMES: Readonly<Record<PageTargetDto, string>> = {
  next: 'Next page',
  previous: 'Previous page',
  specific: 'A specific page',
};

export const PAGE_TARGETS = Object.keys(PAGE_TARGET_NAMES) as PageTargetDto[];

/** Turns modifiers on or off, keeping them unique and in display order. */
export function toggleModifier(
  modifiers: readonly KeyModifierDto[],
  modifier: KeyModifierDto,
  on: boolean,
): KeyModifierDto[] {
  return MODIFIERS.filter((candidate) => (candidate === modifier ? on : modifiers.includes(candidate)));
}

/**
 * The action a key gets when its type changes. A profile switch targets another profile when there is one, else the
 * key's own profile (which the agent allows).
 */
export function defaultAction(
  type: EditableActionType,
  profileId: string,
  profiles: readonly ProfileSummaryDto[],
): ActionDto {
  switch (type) {
    case 'hotkey':
      return { type: 'hotkey', modifiers: ['ctrl'], key: 'c' };
    case 'mediaKey':
      return { type: 'mediaKey', key: 'playPause' };
    case 'changePage':
      return { type: 'changePage', target: 'next', pageIndex: null };
    case 'switchProfile':
      return { type: 'switchProfile', profileId: profiles.find((p) => p.id !== profileId)?.id ?? profileId };
  }
}

/** One line describing what a key does, for the editor. */
export function actionSummary(action: ActionDto, profiles: readonly ProfileSummaryDto[]): string {
  switch (action.type) {
    case 'hotkey':
      return [...action.modifiers.map((modifier) => MODIFIER_NAMES[modifier]), VIRTUAL_KEY_NAMES[action.key]].join(' + ');
    case 'mediaKey':
      return MEDIA_KEY_NAMES[action.key];
    case 'changePage':
      return action.target === 'specific' ? `Go to page ${(action.pageIndex ?? 0) + 1}` : PAGE_TARGET_NAMES[action.target];
    case 'switchProfile': {
      const target = profiles.find((profile) => profile.id === action.profileId);
      return target ? `Switch to ${target.name}` : 'Switch to a missing profile';
    }
    case 'launchApp':
      return 'Launch an app';
    case 'openUrl':
      return `Open ${action.url}`;
    case 'obsScene':
      return `OBS scene ${action.sceneName}`;
    case 'launchGame':
      return `Launch a ${action.store === 'steam' ? 'Steam' : 'Epic'} game`;
  }
}

const MEDIA_ICONS: Readonly<Record<MediaKeyDto, string>> = {
  playPause: 'play-pause',
  next: 'skip-next',
  previous: 'skip-previous',
  stop: 'stop',
  volumeUp: 'volume-up',
  volumeDown: 'volume-down',
  mute: 'volume-mute',
};

/** The bundled icon that fits an action, offered when the action changes. */
export function suggestedIcon(action: ActionDto): string {
  switch (action.type) {
    case 'mediaKey':
      return MEDIA_ICONS[action.key];
    case 'hotkey':
      return 'keyboard';
    case 'changePage':
      return action.target === 'previous' ? 'page-previous' : 'page-next';
    case 'switchProfile':
      return 'layers';
    case 'obsScene':
      return 'scene';
    case 'launchGame':
      return 'gamepad';
    case 'launchApp':
    case 'openUrl':
      return 'lightning';
  }
}
