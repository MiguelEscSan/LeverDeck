# Phase 7 — Make it usable

## Objective

Everything a daily user needs beyond the MVP. Spec: [docs/ROADMAP.md](../../docs/ROADMAP.md) Phase 7; mocks in
[docs/mocks/desktop.html](../../docs/mocks/desktop.html).

## Decisions and defaults

- **The PC edits, devices read.** Layouts change only in the desktop editor (`/admin`). The agent already enforces it:
  `saveProfile` and `deleteProfile` need the admin scope, and `profileUpdated`/`profileDeleted` reach every connection,
  so phones redraw on their own. No agent or protocol change was needed.
- **Save on every change.** Each edit applies on screen at once and is sent as `saveProfile` with the whole profile,
  one save after another. A refused save puts back the last version the agent accepted and shows the agent's reason.
  Text fields (profile name, key label) save when the field is left or Enter is pressed, not per keystroke.
- **Select, then edit.** Choosing a key opens it in the "Selected key" panel; choosing an empty position offers "Add a
  key here" (no accidental keys). Drag and drop comes later.
- **Editable actions for now:** hotkey, media key, change page, switch profile. Launch app, open URL, OBS scene and
  launch game keep their value and show read-only until their pickers exist.
- **Grid:** 3×3, 5×3 and 8×4 (the agent's only sizes); new profiles take the agent's `defaultGrid` setting (5×3 out
  of the box). Shrinking is refused while keys would fall outside.
- **Icons:** a bundled set of 23 icons in `web/src/app/core/keys/domain/key-icons.ts`, shared by the deck and the
  editor; an unknown id still renders as a monogram. An icon that still matches the key's action follows a new action.
- **Links, games and apps (P7-2):** the agent opens links and store URIs through the Windows shell and starts only
  registered executables, by the path stored in the settings, with no arguments. Installed games come from the
  launchers' own manifests (`getInstalledGames`, admin scope); a game the launchers do not list can still be entered
  by store id. Unlabeled keys take the app, game or site name. OBS is postponed (the owner does not use it).
- **Moving keys (P7-3):** drag a key onto another position (a key there trades places) or onto a page number (first
  free position of that page); Alt + arrow keys do the same from the keyboard. The default grid setting stays 5×3 and
  is not editable yet.
- **Ids:** profile and key ids are UUIDs made in the browser from `crypto.getRandomValues` (works without a secure
  context).

## Tasks

| ID | Task | Route | Status |
|---|---|---|---|
| P7-1 | Web: desktop editor — profiles sidebar (create, select), profile toolbar (rename, color, grid size, delete), grid editor with pages (add, remove), selected-key panel (label, color, icon, action) | delegated writer | done (manual check pending) |
| P7-2 | More actions: open URL, launch registered apps, Steam/Epic games with the installed-games list (agent + web); OBS postponed | delegated writer | done (manual check pending) |
| P7-3 | Drag and drop keys between positions and pages, with a keyboard alternative | delegated writer | done (manual check pending) |
| P7-4 | Tray icon, start with Windows, firewall rule on the private profile | — | pending |
| P7-5 | Optional HTTPS over LAN with `mkcert` | — | pending |

## Acceptance

- Tests green; lint and build clean.
- Manual (owner): edit a key in `/admin` and see the paired phone update without reloading; the phone has no way to
  edit.

## Progress

- 2026-10-22: P7-1 done (web only). 293 web tests, lint and build green. Smoke-tested in Chromium against a fake
  agent: select and edit a key (label, color, icon, hotkey with modifiers), add a key, refused grid shrink, new
  profile, switching profiles, narrow layout. Not run against the real agent here (no .NET in that environment).
- 2026-10-22: P7-2 and P7-3 done. Agent: `AgentActionExecutor` (shell for links, games and apps), `GetInstalledGames`
  with `InstalledGameLibrary` (Steam VDF and Epic manifests), protocol `getInstalledGames`/`installedGames` and the
  regenerated TypeScript types. Built and tested with the .NET 10.0.402 SDK on Linux: no new failures (the Linux-only
  failures are the same Windows-path, DPAPI and embedded-file tests as before); CI runs them on Windows. Web: action
  editor for links, apps and games, an Apps card, drag and drop; 331 tests, lint and build green, and smoke-tested in
  Chromium against a fake agent (real drag and drop, Alt + arrows, swap, move to another page). Manual: press a link,
  a Steam/Epic game and an app key on the phone and see them open on the PC.
