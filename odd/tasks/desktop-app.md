# Desktop app: installer and own window

Locator: `odd/tasks/desktop-app.md` · Engram mirror: `odd/desktop-app/tasks` · Branch: `feat/desktop-app`

## Objective

LeverDeck behaves like Spotify or Discord: a setup program installs it, the Start menu opens its own window with the
existing editor (no browser, no URL, no console), and closing the window keeps the agent in the tray for the phones.

## Problem / why

Today `leverdeck.exe` is a console app (no `OutputType`, so the SDK builds the console subsystem): a cmd window opens,
the editor needs a token URL copied from the log into a browser, and there is no installer.

## Decisions (user, 2026-10-23)

- Option A: show the existing Angular editor in a native window (WebView2), not a rewrite in a native UI toolkit.
- No console window for users.
- Installer: Velopack, per-user install with updates from GitHub Releases.
- Logs: every start of the agent (Windows sign-in, or quit from the tray and open again) deletes all existing logs and
  begins a new one; no previous log is kept; a size cap applies within a session. Closing only the window is not a
  start.

## Scope

ROADMAP Phase 8b: spike, GUI subsystem + file logs, editor window, single instance, start-at-login behaviour,
Velopack installer and updates, WebView2 runtime check.

Out of scope: macOS (Phase 9), a native UI rewrite, code signing.

## Constraints

- Keep the agent, the PWA and the protocol unchanged; the window only hosts the editor.
- The admin token never appears to the user.
- Native AOT preferred; single-file self-contained only if the spike shows WebView2 cannot run under AOT.

## Tasks

- [x] T1 — Spike: WebView2 window under Native AOT vs the official package + single-file. Route: delegated worker in
      the scratchpad (no repository changes). Result: AOT works (static loader, 1.9 MB, exit 0 in 6/6 runs, 0 warnings);
      WinForms single-file works too (117 MB, 49 MB working set). Chosen: Native AOT. Details in ROADMAP Phase 8b.
- [ ] T2 — GUI subsystem: `OutputType` `WinExe` in Release, `Exe` otherwise (`LeverDeck.App.csproj`). Built; Windows
      check pending.
- [ ] T3 — File logs: `Infrastructure/Logging` (`SessionLogFile`, `FileLoggerProvider`), Release only; every start
      deletes `*.log` in `<DataDirectory>/logs` and opens an empty `leverdeck.log`; 10 MB cap per run, then a notice
      and nothing more (stop, not truncate: the start of a session is kept). Release no longer logs the admin token.
      Built and unit-tested; Windows check pending.
- [ ] T4 — Editor window: `App/Desktop` (`WebView2.cs` source-generated COM, `NativeEditor.cs`, `EditorWindow.cs`),
      STA tray thread hosting it, per-monitor v2 DPI, close hides, "Open LeverDeck" (default item, also a left click)
      and "Open in browser" in the tray, placement in `window.json` (`Infrastructure/Desktop/WindowPlacementStore`),
      WebView2 profile in `<DataDirectory>/webview2`, missing-runtime message. Built and AOT-compiled; Windows check
      pending.
- [ ] T5 — Single instance and sign-in start: `Local\LeverDeck.Agent` mutex, second launch posts `WmShowEditor` to the
      running tray window; Run key writes `"<exe>" --background`; decision in `Desktop/Launch.cs` (tested). Windows
      check pending.
- [ ] T6 — Velopack: `VelopackApp.Build().Run()` first in `Main`, uninstall hook removes the Run value, `UpdateCheck`
      (installed copies only, GitHub Releases, applied at next start), release workflow packs with `vpk` 1.2.161 and
      uploads `LeverDeckSetup.exe` and Velopack's files next to `leverdeck.exe`. Windows and release checks pending.
- [ ] T7 — Manual check on Windows by the owner (checklist in the PR).

## Progress

- Branch `feat/desktop-app` created from `main` (72209f4). ROADMAP Phase 8b written.
- 2026-10-23: T2–T6 built on branch `claude/amazing-curie-nxci18` from `main` (7d3dbda), one commit per unit, in a
  Linux container (no Windows, no WebView2, no tray, no MSVC linker).

## Evidence (Linux container, .NET SDK 10.0.402)

- `dotnet build LeverDeck.slnx -p:EnableWindowsTargeting=true`, Debug and Release: 0 warnings, 0 errors (warnings
  are errors; AOT/trim analyzers on through `PublishAot`; COM and LoggerMessage source generators ran).
- `dotnet msbuild src/LeverDeck.App -getProperty:OutputType`: `Exe` in Debug, `WinExe` in Release.
- Native AOT compile for win-x64 cross-OS (`dotnet publish -c Release -r win-x64 -p:DisableUnsupportedError=true`):
  ILC generated native code for the whole app, Velopack included, with **no trim or AOT warnings**; only the link step
  failed (no MSVC `link.exe` on Linux). The link response file has `/SUBSYSTEM:WINDOWS`,
  `WebView2LoaderStatic.lib` and `version.lib`, and the object file references
  `CreateCoreWebView2EnvironmentWithOptions` directly (static loader).
- `version.lib` needs no entry of ours: it is in Native AOT's default Windows libraries
  (`Microsoft.NETCore.Native.Windows.targets`, ILCompiler 10.0.13).
- IIDs and vtable slots checked against `WebView2.h` of `Microsoft.Web.WebView2` 1.0.4258.31: environment
  `CreateCoreWebView2Controller` (slot 1 after IUnknown), controller `put_IsVisible` 2, `put_Bounds` 4, `MoveFocus` 10,
  `Close` 22, `get_CoreWebView2` 23, `ICoreWebView2.Navigate` 3.
- Velopack: its csproj at the 1.2.161 commit sets `IsAotCompatible` for net8.0+, and the ILC run above was clean.
  `vpk [win] pack` (cross-pack from Linux) of a JIT win-x64 publish verified `VelopackApp.Run()` in `Main` and
  produced `LeverDeck-win-Setup.exe`, `LeverDeck-<v>-full.nupkg`, `RELEASES`, `releases.win.json`,
  `assets.win.json`; packing a native (non-.NET) exe skips that check without failing, so the AOT exe packs as is.
- Tests: 16 new logging tests, 9 window placement tests, 3 editor URL tests, 12 launch/instance tests pass. The rest of
  the suite matches the `main` baseline on Linux: the same 109 tests fail there for Linux-only reasons (Windows paths,
  DPAPI, the embedded `wwwroot` manifest), plus two of this branch's that need Windows for the same reasons:
  `LoggingTests.Startup_LogsToTheDataDirectoryOnlyInReleaseBuilds` (starts the host: DPAPI) and the renamed
  `StartupRegistrationTests.CommandQuotesThePathOfThePublishedExecutableAndStartsItInTheBackground` (Windows path).
  Windows CI runs them.

## Not verified (needs Windows)

- The AOT link and the published exe; no console window; the editor window, close to tray and reopen, DPI moves
  between monitors, focus and keyboard in the web view, window placement across restarts.
- Single instance (second launch shows the window; a second sign-in start exits), sign-in start stays hidden.
- Logs deleted on every start and capped at 10 MB; the log has no admin token.
- The missing-runtime message (only seen by uninstalling WebView2 or on a PC without it).
- The release workflow (`vpk` on the runner), the installer: install, Start menu shortcut, update to a newer tag,
  uninstall (Run value removed, data kept).
- **Updates need public releases:** the repository is private, so the unauthenticated update check gets 404 and logs
  a warning once a day. Decide: make the repository (or a releases-only repository) public, or keep updates manual.

## Next step

Owner's Windows check (PR checklist), then follow-ups: delta packages (`vpk download github` before `pack`), a
"Restart to update" tray item, `NewWindowRequested`/external links and the browser accelerator keys in the web view,
`WM_ENDSESSION` handling.
