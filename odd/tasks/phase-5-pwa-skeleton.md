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
| P5-1 | Scaffold `web/` (Angular + PWA + ESLint boundaries), TS conventions in `.editorconfig` | delegated writer | pending |
| P5-2 | Theme tokens (light/dark) and an app shell with the connection status | delegated writer | pending |
| P5-3 | `core/connection`: WebSocket service with backoff and a status signal, tests with a fake socket | delegated writer | pending |
| P5-4 | Embed the build in the agent (`ManifestEmbeddedFileProvider`, SPA fallback), CI web job, docs | delegated writer | pending |

## Acceptance

- `npm run lint`, `npm test` and `npm run build` pass; the agent builds, tests and AOT-publishes with the web build
  embedded; `GET /` on the agent returns the app.
- Manual (owner): open `http://leverdeck.local:8765` (or `http://<pc-ip>:8765`) on the phone.

## Progress

- 2026-10-22: document created.
