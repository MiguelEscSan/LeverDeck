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

## Scope

ROADMAP Phase 8b: spike, GUI subsystem + file logs, editor window, single instance, start-at-login behaviour,
Velopack installer and updates, WebView2 runtime check.

Out of scope: macOS (Phase 9), a native UI rewrite, code signing.

## Constraints

- Keep the agent, the PWA and the protocol unchanged; the window only hosts the editor.
- The admin token never appears to the user.
- Native AOT preferred; single-file self-contained only if the spike shows WebView2 cannot run under AOT.

## Tasks

- [ ] T1 — Spike: WebView2 window under Native AOT vs the official package + single-file. Route: delegated worker in
      the scratchpad (no repository changes). Named uncertainty: whether WebView2's COM API works with `ComWrappers`
      under AOT.
- [ ] T2+ — Planned after the spike (GUI subsystem, window, single instance, installer).

## Progress

- Branch `feat/desktop-app` created from `main` (72209f4). ROADMAP Phase 8b written.

## Next step

T1 spike.
