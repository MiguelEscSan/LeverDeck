# Phase 5 — PWA skeleton (Angular)

## Objective

An Angular PWA in `web/`, built and embedded into `leverdeck.exe`, with the WebSocket connection core and the visual
tokens from [docs/VISUAL-REFERENCE.md](../../docs/VISUAL-REFERENCE.md). Spec: [docs/ROADMAP.md](../../docs/ROADMAP.md)
Phase 5.

## Decisions and defaults

- Angular CLI 22 (latest stable), standalone, strict, zoneless if the CLI default; npm; the CLI's default unit test
  runner.
- Keep the generated protocol types in `web/src/app/core/protocol/` (never hand-edited).
- No features yet beyond a shell showing the connection state; pairing and deck are Phase 6.
- The agent serves the embedded build with an SPA fallback; the `/ws` endpoint itself arrives in Phase 6, so the
  connection service is tested against a fake socket.
- Delivery: branch `feat/phase-5-pwa` pushed; the owner merges.

## Tasks

| ID | Task | Route | Status |
|---|---|---|---|
| P5-1 | Scaffold `web/` (Angular + PWA + ESLint boundaries), TS conventions in `.editorconfig` | delegated writer | done |
| P5-2 | Theme tokens (light/dark) and an app shell with the connection status | delegated writer | done |
| P5-3 | `core/connection`: WebSocket service with backoff and a status signal, tests with a fake socket | delegated writer | done |
| P5-4 | Embed the build in the agent (`ManifestEmbeddedFileProvider`, SPA fallback), CI web job, docs | delegated writer | done |

## Acceptance

- `npm run lint`, `npm test` and `npm run build` pass; the agent builds, tests and AOT-publishes with the web build
  embedded; `GET /` on the agent returns the app.
- Manual (owner): open `http://leverdeck.local:8765` (or `http://<pc-ip>:8765`) on the phone.

## Progress

- 2026-10-22: document created.
- 2026-10-22: Phase 5 done. Web: 50 tests, lint clean (boundaries proven with probe files), build 56 kB transferred.
  Agent: 870 tests green; AOT exe 16.1 MB with the app embedded, no warnings; `/`, a client route, the hashed bundle and
  the manifest served with the right cache headers.
  - Angular CLI 22.2.4: zoneless, standalone, Vitest, SCSS. Shell in `features/shell`; connection in `core/connection`
    (domain/application/infrastructure). The badge shows "Reconnecting…" until `/ws` exists (Phase 6).
  - Missing web build embeds a fallback page instead of failing; `-p:BuildWebUi=true` builds web first,
    `-p:RequireWebUi=true` (used in CI) makes a missing build an error.
  - SPA fallback only for GET/HEAD outside `/api`, `/ws`, `/admin/ws`, `/debug`, `/health`.
  - **Open:** dark muted grey `#a0a0a0` on panel `#3a3a3a` is ~4.35:1 (below AA 4.5:1); unused so far, fix before it is.
  - **Pending manual (owner):** load the PWA on the phone (see the checks in the Phase 5 report summary in chat).
