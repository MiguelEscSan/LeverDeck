# Phase 6 — First vertical slice: pair and press (MVP)

## Objective

A freshly paired phone presses a key and the PC receives the input. Spec: [docs/ROADMAP.md](../../docs/ROADMAP.md)
Phase 6; wire format in Phase 3; mocks in [docs/mocks/](../../docs/mocks/).

## Decisions and defaults

- **QR flow:** the phone scans the QR with its own camera app, which opens `http://leverdeck.local:8765/pair?code=…`
  (plus an IP fallback URL). No in-app camera scanner: `getUserMedia` needs a secure context, which plain-HTTP LAN is
  not. Typing the 6-digit code is the alternative.
- **QR rendering:** in the browser with a small pinned MIT library (no server-side image).
- **Admin editor:** served by the same PWA under `/admin`; it reads `?admin=<token>` once, keeps it in
  `sessionStorage`, removes it from the address bar, and connects to `/admin/ws` with it.
- **Device token:** kept in `localStorage` on the phone; sent in `connectDevice` as the first `/ws` message.
- **Transport:** the agent pushes events to every live connection of the right scope; `deviceRevoked` closes the
  revoked device's sockets. The OBS-scene event stays Phase 7.
- Delivery: branch `feat/phase-6-pair-and-press` pushed; the owner merges.

## Tasks

| ID | Task | Route | Status |
|---|---|---|---|
| P6-1 | Agent: `POST /api/pairing/redeem`, `/ws` (device) and `/admin/ws` (admin) endpoints, connection registry, event fan-out, revocation closes sockets | delegated writer | pending |
| P6-2 | Web: `features/pairing` (code entry + `/pair?code=` landing), `features/deck` (grid + press), `features/admin-pairing` (QR, code, countdown, devices list with revoke) | delegated writer | pending |

## Acceptance

- Tests green on both sides; AOT publish clean; lint clean.
- Manual (owner): pair the phone over LAN and over USB (`adb reverse tcp:8765 tcp:8765`), press a key, see it on the PC.

## Progress

- 2026-10-22: document created.
