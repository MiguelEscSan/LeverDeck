# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Decided in docs/HANDOFF.md: Angular PWA in `web/` (standalone, Signals, OnPush) served by a .NET 10 agent in `agent/` (ASP.NET Core minimal APIs on Kestrel). The PWA is embedded in the single executable.

## Users

Primary: someone who already uses a PC for streaming, video editing, media or games, and wants a phone or tablet as a Stream Deck-style control surface. Confirmed with the owner: the product is open source and other people will install it, so onboarding must be understandable without the author present.

## Product Purpose

Tapping a button on a phone or tablet triggers an action on the PC: hotkeys, media keys, launching apps, OBS scenes, games. It is a free, self-hosted alternative to the Elgato Stream Deck. Success means a new user pairs their device once and their deck just works afterwards.

## Positioning

Any device with a modern browser becomes a control surface, with no app store, no paid certificate and no cloud. The PC is the source of truth and the actions are a closed, whitelisted set.

## Operating Context

- Used beside the PC, during live streams, recording sessions, editing and gaming, often with one hand.
- Pairing goes through a QR code on the PC; connection is over LAN by the PC's own name (`leverdeck-<pc>.local`, so several PCs can share a network) or over USB with `adb reverse`.
- Profiles group keys per use case (streaming, editing, media, games).

## Capabilities and Constraints

- Windows agent only. macOS is planned but not implemented yet.
- Actions: hotkeys, media keys, launch app, OBS scene, planned game launch via Steam and Epic. Arbitrary commands are never allowed.
- Connection states must be visible: connected, reconnecting, offline (PC unreachable).
- Undecided: final key-grid size, number of profiles shown at once, whether a light theme is required by users beyond the system preference.

## Brand Commitments

The name is LeverDeck. No logo exists yet. The mobile app's look and feel follows docs/VISUAL-REFERENCE.md
(dark charcoal surface, saturated rounded keys, Stream Deck Mobile–inspired, without Elgato branding).

## Evidence on Hand

- docs/ARCHITECTURE.md, CLAUDE.md, README.md and docs/HANDOFF.md.
- docs/VISUAL-REFERENCE.md with the reference screenshot in docs/assets/.
- The prototype in docs/prototype/index.html (screens only, no real data).
- No photographs, customer proof or testimonials exist. Do not invent any.

## Product Principles

- Pairing once is enough; reconnecting must be automatic and quiet.
- The deck is for hands in motion: large targets, clear state, no hidden actions.
- Only paired devices can act, and only from the actions set up on the PC.
- Open source means the first run explains itself.

## Accessibility & Inclusion

Keys and controls must reach WCAG AA contrast in both themes. Respect the system's reduced-motion and color-scheme preferences.
