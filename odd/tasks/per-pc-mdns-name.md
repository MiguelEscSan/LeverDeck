# Per-PC mDNS name: several PCs on one LAN

Locator: `odd/tasks/per-pc-mdns-name.md` · Engram mirror: `odd/per-pc-mdns-name/tasks` · Branch: `feat/per-pc-mdns-name`

## Objective

Each agent advertises and answers for its own stable name, `leverdeck-<pc>.local`, so several PCs can run LeverDeck on
the same LAN and a phone pairs with, and connects to, the one it chose.

## Problem / why

Every agent uses the hard-coded `leverdeck.local` (`PairingLinks.HostName`, `LanCertificate.HostName`,
`DnsServiceAdvertiser.HostName`) and the same DNS-SD instance `LeverDeck._http._tcp.local`. With two PCs both answer,
the phone reaches whichever answers first, and because the PWA keeps its token per origin, pairing with a second PC
overwrites the first.

## Decisions (user, 2026-10-23)

- Readable name from the PC name, generated once and stored; override with `LeverDeck__Mdns__HostName`.
- Drop `leverdeck.local` entirely; paired devices re-pair once.
- T6 (show the PC name on the deck and in the editor) is left for later.

## Scope

- `agent/src/LeverDeck.Infrastructure`: host name rule and store, options, DI, `PairingLinks`, `LanCertificate`,
  `DnsServiceAdvertiser`, `MdnsHostNameResponder`, the conflict warning; their tests.
- `web/`: the two `'leverdeck.local'` fallback literals and their specs.
- Docs: `docs/ARCHITECTURE.md`, `README.md`, `docs/ROADMAP.md` where they describe the name.

Out of scope: protocol changes (T6), mDNS probing (RFC 6762 §8), migration of paired devices.

## Constraints

- OS calls (`Environment.MachineName`, files) stay in Infrastructure; the naming rule is pure and tested first.
- Native AOT: no reflection binder for options.
- English for code and docs; Conventional Commits.

## Acceptance criteria

- `DESKTOP-Miguel Ñ` → `leverdeck-desktop-miguel-n.local`; labels ≤ 63 chars, only `[a-z0-9-]`, no leading/trailing
  `-`; empty after cleaning → random suffix.
- The name is written once to `<DataDirectory>/hostname` and reused; a valid override wins; an invalid stored name is
  replaced; an invalid override fails startup with a clear message.
- mDNS responder, DNS-SD registration (target and instance), QR link and LAN certificate all use that one name.
- A conflicting responder warning names the override setting.
- No `leverdeck.local` literal left in agent or web sources (tests may keep codec samples).

## Checks

- From `agent/`: `dotnet build LeverDeck.slnx`, `dotnet test LeverDeck.slnx`.
- From `web/`: `npm run lint`, `npm test`.

## Delivery

Strategy: `ask-on-risk`. Forecast about 350 authored changed lines.

## Tasks

- [x] T1 — Host name rule (`AgentHostName`), test-first. Route: delegated writer (with T2–T4).
      RED: CS0103 (no `AgentHostName`); then 2 failures because `InvariantGlobalization` makes `Normalize(FormD)` a
      no-op, fixed with an explicit Latin fold table; GREEN 20/20.
- [x] T2 — Stored name + override (`AgentHostNameStore`, `LeverDeckOptions`). Route: delegated writer.
      RED: CS0246 (no `AgentHostNameStore`); GREEN: store + options tests pass.
- [x] T3 — One source for responder, advertiser, pairing links, certificate. Route: delegated writer (2+ non-trivial
      files fire the writer trigger). DNS-SD instance `<label>._http._tcp.local`; app tests pin
      `LeverDeck:Mdns:HostName=leverdeck-test`.
- [x] T4 — Conflict warning names the override. Route: delegated writer. New responder test checks the warning.
      Checks: `dotnet build LeverDeck.slnx` clean; `dotnet test LeverDeck.slnx` 1156 passed.
- [ ] T5 — Web fallback literals and docs. Route: inline (mechanical edits).

## Progress

- Branch `feat/per-pc-mdns-name` created from `main` (d66933c).

## Next step

T1–T4 via one writer.
