/** Limits of a registered app, as the agent's RegisteredApp enforces them. */
export const APP_NAME_MAX_LENGTH = 40;
export const APP_PATH_MAX_LENGTH = 260;

/**
 * Cleans a pasted executable path: Windows' "Copy as path" wraps it in quotes, which the agent refuses (nothing may
 * be appended to the command), so surrounding quotes and spaces go.
 */
export function cleanPath(text: string): string {
  return text.trim().replace(/^"(.*)"$/, '$1').trim();
}

/** A name for an app from its executable: the file name without `.exe` ("C:\…\obs64.exe" → "obs64"). */
export function nameFromPath(path: string): string {
  const file = cleanPath(path).split(/[\\/]/).pop() ?? '';
  return file.replace(/\.exe$/i, '').slice(0, APP_NAME_MAX_LENGTH);
}

/**
 * Why an app would be refused, or `null`: a name of 1–40 characters and a full Windows path (drive or network share)
 * of at most 260 characters with no quotes. The agent checks again and refuses a path that is already registered.
 */
export function appFormError(name: string, path: string): string | null {
  const trimmedName = name.trim();
  if (trimmedName.length === 0 || trimmedName.length > APP_NAME_MAX_LENGTH) {
    return `Give the app a name of 1–${APP_NAME_MAX_LENGTH} characters.`;
  }
  const cleaned = cleanPath(path);
  if (cleaned.length === 0) {
    return 'Paste the full path of the program, like C:\\Program Files\\App\\app.exe.';
  }
  if (cleaned.length > APP_PATH_MAX_LENGTH) {
    return `Use a path of at most ${APP_PATH_MAX_LENGTH} characters.`;
  }
  if (cleaned.includes('"')) {
    return 'The path cannot contain quotes or arguments.';
  }
  if (!/^(?:[A-Za-z]:\\|\\\\[^\\]+\\[^\\]+)/.test(cleaned)) {
    return 'Use the full path, starting with a drive like C:\\.';
  }
  return null;
}
