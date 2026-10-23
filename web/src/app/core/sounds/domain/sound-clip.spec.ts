import {
  formatDuration,
  formatSize,
  SOUND_MAX_BYTES,
  soundIdFromUuid,
  soundNameError,
  soundNameFromFile,
  soundUploadError,
} from './sound-clip';

describe('sound clips', () => {
  it('shows a length as minutes and seconds', () => {
    expect(formatDuration(42_000)).toBe('0:42');
    expect(formatDuration(130_000)).toBe('2:10');
    expect(formatDuration(0)).toBe('0:00');
  });

  it('shows a size in megabytes, or kilobytes for a tiny clip', () => {
    expect(formatSize(4_404_019)).toBe('4.2 MB');
    expect(formatSize(314_573)).toBe('0.3 MB');
    expect(formatSize(20_480)).toBe('20 KB');
  });

  it('turns a UUID into the id the agent expects', () => {
    expect(soundIdFromUuid('0123ABCD-89ab-4def-8123-456789ABCDEF')).toBe('sound-0123abcd89ab4def8123456789abcdef');
  });

  it('takes the name of a file without its extension and trims it to the limit', () => {
    expect(soundNameFromFile('  applause-short.wav ')).toBe('applause-short');
    expect(soundNameFromFile('a.b.wav')).toBe('a.b');
    expect(soundNameFromFile('x'.repeat(80) + '.wav')).toHaveLength(60);
  });

  it('refuses a name that is empty, too long or has control characters', () => {
    expect(soundNameError('   ')).toMatch(/1–60/);
    expect(soundNameError('x'.repeat(61))).toMatch(/1–60/);
    expect(soundNameError('tab\there')).toMatch(/control/);
    expect(soundNameError('Applause')).toBeNull();
  });

  it('accepts a WAV file up to 50 MB and refuses the rest with the reason', () => {
    expect(soundUploadError({ name: 'a.wav', size: SOUND_MAX_BYTES })).toBeNull();
    expect(soundUploadError({ name: 'a.WAV', size: 1 })).toBeNull();
    expect(soundUploadError({ name: 'song.mp3', size: 1 })).toMatch(/WAV/);
    expect(soundUploadError({ name: 'a.wav', size: SOUND_MAX_BYTES + 1 })).toMatch(/limit 50 MB/);
  });
});
