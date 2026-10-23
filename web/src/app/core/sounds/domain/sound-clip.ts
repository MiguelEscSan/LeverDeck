/** Largest clip the agent accepts, as it checks it too (50 MB). */
export const SOUND_MAX_BYTES = 50 * 1024 * 1024;

const SOUND_NAME_MAX = 60;

/** Length as the editor shows it: minutes and seconds, such as `0:42` or `2:10`. */
export function formatDuration(durationMs: number): string {
  const seconds = Math.floor(durationMs / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

/** Size as the editor shows it: megabytes with one decimal, or whole kilobytes for a very small clip. */
export function formatSize(bytes: number): string {
  const megabytes = bytes / (1024 * 1024);
  return megabytes >= 0.1 ? `${megabytes.toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/** The id of a new clip: `sound-` and the UUID's 32 hex digits, which is the shape the agent accepts. */
export const soundIdFromUuid = (uuid: string): string => `sound-${uuid.replace(/-/g, '').toLowerCase()}`;

/** The name a file suggests: its file name without the extension, trimmed to the name limit. */
export function soundNameFromFile(fileName: string): string {
  return fileName.replace(/\.[^.]*$/, '').trim().slice(0, SOUND_NAME_MAX);
}

/** Why a name would be refused, or `null`: 1–60 characters once trimmed, no control characters. */
export function soundNameError(name: string): string | null {
  const trimmed = name.trim();
  if (trimmed.length < 1 || trimmed.length > SOUND_NAME_MAX) {
    return `Use 1–${SOUND_NAME_MAX} characters.`;
  }
  const hasControl = [...trimmed].some((char) => {
    const code = char.charCodeAt(0);
    return code < 0x20 || code === 0x7f;
  });
  return hasControl ? 'The name must not contain control characters.' : null;
}

/**
 * Why a file cannot be uploaded, or `null`. Only uncompressed WAV plays on the PC, so MP3, OGG and FLAC are refused
 * with the way out; the size limit is the agent's.
 */
export function soundUploadError(file: { readonly name: string; readonly size: number }): string | null {
  if (!/\.wav$/i.test(file.name)) {
    return 'Only WAV files can play. Convert MP3, OGG or FLAC to WAV first.';
  }
  if (file.size > SOUND_MAX_BYTES) {
    return `Too large: ${formatSize(file.size)} (limit 50 MB).`;
  }
  return null;
}
