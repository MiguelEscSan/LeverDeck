# App icon on the exe and a GitHub Release

Locator: `odd/tasks/exe-icon-release.md` · Engram mirror: `odd/exe-icon-release/tasks` · Branch: `feat/exe-icon-release`

## Objective

`leverdeck.exe` shows the app icon (the four-key grid of the PWA) in Explorer, the taskbar and the tray, and a version
tag publishes the exe as a GitHub Release from CI.

## Problem / why

The exe has no icon of its own and the tray uses Windows' generic `IDI_APPLICATION`. CI builds the Native AOT exe on
every push to `main` but only uploads it as a workflow artifact; there is no release (ROADMAP Phase 8).

## Scope

- `agent/src/LeverDeck.App/leverdeck.ico` (new, from `web/public/icons/icon-512x512.png`), `ApplicationIcon` in
  `LeverDeck.App.csproj`, the tray loading the exe's own icon (`TrayIcon.cs`).
- `.github/workflows/release.yml` (new): on a `v*` tag, build, test, publish Native AOT and create the release.
- ROADMAP Phase 8 item.

Out of scope: the README install guide (Phase 8, separate), code signing, an installer.

## Constraints

- Native AOT: the icon must reach the native exe, not only the managed assembly.
- Pushing a tag publishes a public release: only on the user's explicit OK.
- Conventional Commits.

## Acceptance criteria

- The published exe carries the icon (Explorer shows it); the tray shows it, falling back to the system icon.
- A pushed `v*` tag runs the release workflow, which attaches `leverdeck.exe` to a release named after the tag.

## Checks

- From `agent/`: `dotnet build LeverDeck.slnx`, `dotnet test LeverDeck.slnx`; the built exe carries an icon.
- Release workflow: runs on the first tag push (only verifiable on GitHub).

## Tasks

- [x] T1 — Exe and tray icon. Route: inline (one generated binary + two small edits). `ea66b53`. Checks: the Debug
      exe and a local Native AOT publish both carry icon group 32512 (FindResource); `dotnet test` 1157 passed. No
      unit test: the tray icon is a Win32 call with no testable seam; the fallback keeps the old behaviour.
- [x] T2 — Release workflow on `v*` tags. Route: inline (one new workflow file). Parses as YAML; tag `-` suffix makes
      a pre-release; `-p:Version` from the tag. Only verifiable on the first tag push.

## Progress

- Branch `feat/exe-icon-release` created from `main` (dd90d97). No tags exist yet.
- Review (user granted, high risk, 4 lenses): approved and acknowledged; 1 warning and 7 suggestions, all fixed at the
  user's request: release split into a read-only `build` job (`persist-credentials: false`) and a write-only `release`
  job with no package code; actions pinned to SHAs (checkout v4.4.0, setup-node v4.4.0, setup-dotnet v4.3.1,
  upload-artifact v4.6.2, download-artifact v4.3.0); re-runs upload `--clobber` to an existing release; tag checked
  against a semver pattern and trigger narrowed to `v[0-9]*`; one `PUBLISH_DIR` / `DOWNLOAD_DIR`; `OwnIconId` constant;
  tray loads the small frame with `LoadImage` (`LR_SHARED`). Checks: `dotnet build` 0 errors, `dotnet test` 1157
  passed, workflow parses, tag regex accepts `v0.1.0` / `v1.2.3-beta.1` and refuses `vnext` / `v1.2`.

## Next step

Merge into `main`, push, then push the first tag (`v0.1.0`) once the user says so.
