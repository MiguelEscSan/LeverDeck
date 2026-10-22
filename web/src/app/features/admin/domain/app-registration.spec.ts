import { appFormError, cleanPath, nameFromPath } from './app-registration';

describe('cleanPath', () => {
  it('drops the quotes of "Copy as path" and spaces', () => {
    expect(cleanPath('  "C:\\Program Files\\OBS\\obs64.exe" ')).toBe('C:\\Program Files\\OBS\\obs64.exe');
    expect(cleanPath('C:\\Tools\\app.exe')).toBe('C:\\Tools\\app.exe');
  });
});

describe('nameFromPath', () => {
  it('uses the file name without .exe', () => {
    expect(nameFromPath('"C:\\Program Files\\OBS\\obs64.exe"')).toBe('obs64');
    expect(nameFromPath('\\\\nas\\tools\\Editor.EXE')).toBe('Editor');
  });
});

describe('appFormError', () => {
  it('accepts a named app with a full path', () => {
    expect(appFormError('OBS', '"C:\\Program Files\\OBS\\obs64.exe"')).toBeNull();
    expect(appFormError('Tool', '\\\\nas\\share\\tool.exe')).toBeNull();
  });

  it('needs a name', () => {
    expect(appFormError('  ', 'C:\\a.exe')).toBe('Give the app a name of 1–40 characters.');
    expect(appFormError('x'.repeat(41), 'C:\\a.exe')).toBe('Give the app a name of 1–40 characters.');
  });

  it('needs a full path without quotes or arguments', () => {
    expect(appFormError('App', '')).toContain('Paste the full path');
    expect(appFormError('App', 'app.exe')).toBe('Use the full path, starting with a drive like C:\\.');
    expect(appFormError('App', 'C:\\app.exe" --evil')).toBe('The path cannot contain quotes or arguments.');
    expect(appFormError('App', `C:\\${'x'.repeat(260)}`)).toBe('Use a path of at most 260 characters.');
  });
});
