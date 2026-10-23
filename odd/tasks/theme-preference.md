# Theme preference: System, Dark or Light

Locator: `odd/tasks/theme-preference.md` · Engram mirror: `odd/theme-preference/tasks` · Branch: `feat/theme-preference`

## Objective

The PWA follows the system theme by default, and the person can pin it to Dark or Light, both on the phone deck and
in the desktop editor. The choice survives reloads and never flashes the wrong theme on load.

## Problem / why

`web/src/styles.scss` switches to the light tokens only through `prefers-color-scheme`, so a phone in light mode
cannot keep the dark deck (or the other way round).

## Decisions

- Per device, in `localStorage` (`leverdeck.theme`), like the deck preferences. The theme is about the screen in front
  of the person, not about the PC, so it is not synced from the agent.
- Shared code goes in `core/theme` (domain, application, infrastructure); the deck and the editor share it. `core/` has
  no `ui/`, so each feature has its own small presentational control, styled for its own layout.
- `data-theme="dark|light"` on `<html>` pins a theme; no attribute means System. The light tokens apply under
  `prefers-color-scheme: light` unless `data-theme="dark"`, and always under `data-theme="light"`.
- An inline script in `index.html` applies the stored choice before the first paint (no flash); the agent sends no CSP
  that would block it.
- The two `theme-color` metas keep their media queries; a pinned theme writes its colour into both.
- Token values are unchanged, so the documented 4.5:1 text contrast holds in both themes.

## Scope

- New `web/src/app/core/theme/{domain,application,infrastructure}` (+ specs).
- `web/src/styles.scss`, `web/src/index.html`, `web/src/app/app.config.ts`.
- Deck: a theme select in the bar of `deck-page`. Editor: a System/Dark/Light segmented control in the sidebar footer.
- `docs/ARCHITECTURE.md`: the theme line and the `core/theme` module.

Out of scope: agent and protocol changes, new token values.

## Acceptance criteria

- System (default), Dark and Light apply the matching tokens in both views; the choice persists across reloads.
- Blocked or garbage storage falls back to System without throwing.
- `theme-color` follows the applied theme.

## Checks

From `web/`: `npm run lint`, `npm test` (`ng test --watch=false`), `npm run build`.

## Delivery

Strategy: `ask-on-risk`. Forecast about 350 authored changed lines.

## Tasks

- [x] T1 — `core/theme`: preference parsing, theme-color mapping, ThemeFacade, storage and document adapters
      (test-first). Route: inline (context already loaded; small mechanical files). RED: the specs failed to
      compile (no `../domain/theme`); GREEN: `ng test --include core/theme` 12 passed; `npm run lint` passes. jsdom 30
      has no `HTMLMetaElement.media`, so the adapter reads the `media` attribute.
- [x] T2 — Tokens under `data-theme`, no-flash script, providers, controls in the deck and the editor, docs. Route:
      inline (context already loaded). Deck: native `ThemeSelect` at the right of the bar; editor: `ThemeSwitch`
      radio segments in the sidebar footer. Checks: `npm run lint` all pass, `npm test` 391 passed (59 files),
      `npm run build` complete (built index.html keeps the inline script). Browser (dev server, 375x812): pinned light
      under a dark OS and pinned dark under a light OS apply, the choice survives a reload, System restores both
      `theme-color` metas.

## Progress

- Branch `feat/theme-preference` created from `main` (fe815be).
