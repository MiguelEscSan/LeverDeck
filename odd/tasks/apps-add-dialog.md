# Apps: paged list and an "Add app" dialog

Locator: `odd/tasks/apps-add-dialog.md` · Engram mirror: `odd/apps-add-dialog/tasks` · Branch: `feat/apps-add-dialog`

## Objective

The Apps view shows only the registered apps, paged and searchable. Adding an app moves into an "Add app" dialog with
two tabs: Microsoft Store (search + paged list, "Added" for registered ones) and Program (.exe) (path + name form).

## Problem / why

Today one card mixes three things: the registered list, the Store catalog and the .exe form. It is long and it is not
clear which part is what. Approved design: canvas "Apps section redesign" (Main, AddStore, AddProgram, Empty artboards).

## Scope

- `web/src/app/features/admin/domain/store-apps-page.ts` (+ spec): page registered apps by name or path.
- `web/src/app/features/admin/ui/components/app-list/` (+ spec): paged list, search, "Add app" button, empty state.
- New `web/src/app/features/admin/ui/components/add-app-dialog/` (+ spec): the dialog with both tabs.
- `web/src/app/features/admin/ui/pages/admin-page/` spec and wiring as needed.

Out of scope: agent/protocol changes, the right panel, the Icons view.

## Constraints

- Presentational components, standalone + `OnPush`, `inject()`, Signals; no WebSocket in components.
- Same validation as `appFormError`; two-step remove stays.
- English UI copy; Conventional Commits; test-first for the domain change.

## Acceptance criteria

- Registered apps paged (6 per page), filtered by name or path ignoring case and accents, "Showing a–b of n".
- Each row shows a Program / Microsoft Store kind; Store rows show the AppUserModelId, not `shell:AppsFolder\`.
- "Add app" opens a modal dialog; Store tab is the default; tabs switch; Escape and Close dismiss it.
- Registering a program closes the dialog and clears the form; adding a Store app marks it "Added".
- Empty state with its own "Add app" button.

## Checks

From `web/`: `npm run lint`, `npm test`, `npm run build`.

## Delivery

Strategy: `ask-on-risk`. Forecast about 450 authored changed lines (UI rewrite plus specs).

## Tasks

- [x] T1 — Domain: `pageOfApps` for registered apps (test-first). Route: inline (one mechanical file + spec).
      RED: spec failed to compile (no `pageOfApps` export); GREEN: `ng test --include store-apps-page.spec.ts`
      5 passed.
- [ ] T2 — UI: AppList paged list + AddAppDialog with Store and Program tabs, specs, admin page wiring. Route:
      delegated writer (2+ non-trivial files fires the writer trigger).

## Progress

- Branch `feat/apps-add-dialog` created from `main` (f09b111).

## Next step

T1.
