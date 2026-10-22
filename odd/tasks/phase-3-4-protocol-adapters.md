# Phases 3 and 4 — Wire contract and Windows adapters

## Objective

Phase 3: the wire contract in `LeverDeck.Protocol`, DTO ⇄ domain mappers and generated TypeScript types.
Phase 4: the Windows adapters behind the Phase 2 ports, wired in `LeverDeck.App`.
Specs: [docs/ROADMAP.md](../../docs/ROADMAP.md) Phases 3 and 4.

## Decisions (user, 2026-10-22)

- **Admin authentication:** a persistent admin token, generated once and reused across restarts, stored under
  `%APPDATA%\LeverDeck\`. `/admin/ws` requires it. The editor URL carries it (the tray opens it in Phase 7).
- **Gentle AI reviews:** the owner is away; consent prompts are left pending, never answered on the owner's behalf.
- **Delivery:** two stacked branches pushed to origin — `feat/phase-3-protocol` from `main`,
  `feat/phase-4-windows-adapters` on top. The owner opens and merges the PRs (gh is not authenticated).

## Defaults chosen by the agent (revisit if needed)

- TypeScript generation: TypeGen first; if it cannot express the discriminated unions cleanly, a small reflection-based
  generator in `agent/tools/`. Output in `web/src/app/core/protocol/` (the Angular app arrives in Phase 5).
- Add `ActionFailed` to `DomainErrorCode` (executor failures).
- Admin token protected with DPAPI if it is AOT-compatible; otherwise plain file under the user profile.
- Device tokens hashed with SHA-256 (they are high-entropy random values, so no slow KDF is needed).

## Tasks

| ID | Task | Route | Status |
|---|---|---|---|
| P3-1 | Protocol DTOs, envelope, JSON source generation, contract round-trip tests | delegated writer | pending |
| P3-2 | DTO ⇄ domain mappers in Infrastructure, `ActionFailed` code | delegated writer | pending |
| P3-3 | TypeScript generation + snapshot test; admin-auth decision in ARCHITECTURE.md | delegated writer | pending |
| P4-1 | JSON repositories, in-memory pairing store, token generator/hasher, clock, admin token store | delegated writer | pending |
| P4-2 | `SendInput` executor (Hotkey, MediaKey) via CsWin32 + executor-coverage architecture test | delegated writer | pending |
| P4-3 | mDNS advertiser via `DnsServiceRegister`; DI wiring in App; Development-only debug endpoint | delegated writer | pending |

## Acceptance

- Phase 3: every message round-trips; changing a record regenerates the TypeScript; generated files never hand-edited.
- Phase 4: build, tests and Native AOT publish green. Manual checks (a hotkey reaching the PC, `ping leverdeck.local`
  from another device) are left for the owner — they cannot be run unattended.

## Progress

- 2026-10-22: document created; decisions recorded.
