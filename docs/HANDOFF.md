# LeverDeck — Session Handoff

Context for a new session picking up LeverDeck. Read this first, then [ARCHITECTURE.md](ARCHITECTURE.md) and [../CLAUDE.md](../CLAUDE.md).

## What we are building

A free, self-hosted alternative to the Elgato Stream Deck: any phone or tablet becomes a control surface for the PC.
Tapping a button on the device triggers an action on the PC (hotkeys, media keys, launching apps, OBS scenes, games).

## Where

- **Local path:** `C:\Projects\leverdeck`
- **Remote:** https://github.com/MiguelEscSan/LeverDeck (branch `main`)
- **Current state:** documentation only (`README.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`). No code scaffolded yet.

## How (decided)

- **One executable** `leverdeck.exe` in **Rust** (axum + tokio). It serves the PWA and executes the actions.
  The PWA build is embedded into the binary with `rust-embed`.
- **Frontend:** **Angular** PWA (`@angular/pwa`), standalone, Signals, `OnPush`. The user knows Angular well,
  and the UI will grow beyond the grid (profiles, settings, button editor).
- **Architecture:** hexagonal on both sides. Rust uses a cargo workspace with one crate per layer
  (`domain`, `application`, `protocol`, `infrastructure`, `app`). Angular is feature-first
  (`core`, `pairing`, `deck`, `profiles`, `settings`), each with `domain/application/infrastructure/ui`.
- **Light CQRS:** commands vs queries vs pushed events. No buses, mediators, separate read models or event sourcing.
- **Contract:** defined in Rust (`protocol` crate); TypeScript types generated with `ts-rs`.
- **Connectivity:** HTTP + WebSocket on port `8765`. LAN via mDNS name **`leverdeck.local`** (connect by name so the
  browser origin survives IP changes; the user cannot configure the router). USB on Android via `adb reverse`.
  Optional `mkcert` certificate for HTTPS over LAN.
- **Pairing:** QR with a one-time code → long-lived device token stored in the browser; the agent keeps only a hash in
  `devices.json`. Pair once per origin (LAN and USB are separate origins).
- **Persistence:** the PC is the source of truth (profiles/devices/settings JSON in the app config dir).
- **Security:** `Action` is a closed whitelisted enum; never arbitrary commands.
- **Platforms:** Windows agent in scope. macOS agent planned but **not to be implemented yet**; keep OS code behind
  ports in `infrastructure` and resolve paths with the `directories` crate. Devices: anything with a modern browser.
- **Cost:** zero. No app stores, no paid certificates, no cloud.

## Rejected (and why)

- **Docker** to host the PWA: containers cannot inject input into the Windows host, and Docker Desktop + WSL2 is far too heavy.
- **Rust/WASM frontend** (Leptos, Yew): slower builds, bigger bundle, smaller ecosystem; type sharing is covered by `ts-rs`.
- **Native mobile app:** iOS needs a Mac and a paid account to avoid 7-day re-signing; a PWA works everywhere for free.
- **Full CQRS:** single user, JSON storage — no benefit.
- **Fixing IP by MAC / router reservation:** the browser cannot see MACs, and the user has no router access. mDNS name instead.
- **Static IP on the PC:** risk of conflicts on a network the user does not control.

## Future ideas (not scheduled)

- **Launch Steam/Epic games:** `steam://rungameid/<appid>` and `com.epicgames.launcher://apps/<AppName>?action=launch&silent=true`
  (Epic URL format still to be verified). Installed games can be listed from Steam's `libraryfolders.vdf` / `appmanifest_*.acf`
  and Epic's `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item`. Model as `Action::LaunchGame { store, id }` plus a
  `GameLibrary` port; build the URL from a validated id, never accept a free-form URL.
- **OBS integration** through OBS's built-in WebSocket server (v28+).
- **Games that ignore simulated input:** some anti-cheat systems reject keystrokes flagged as injected by `SendInput`
  (`LLKHF_INJECTED`), so hotkeys may not reach those games. A kernel driver is out of scope. Revisit in a later iteration:
  document known affected games and prefer non-input actions (store launch URLs, OBS) where possible.
- **Elevated targets (UIPI):** a non-elevated agent cannot send input to an app running as administrator. Decide later
  whether to detect this and warn the user, or offer an opt-in elevated mode.

## Next steps

1. Add `.gitattributes` (`* text=auto eol=lf`) — Git currently warns about LF/CRLF on Windows.
2. Scaffold the Rust workspace under `agent/` with the five crates and the dependency rule.
3. Scaffold the Angular app under `web/` with `@angular/pwa` and `eslint-plugin-boundaries`.
4. Fill the **Commands** section of `CLAUDE.md` with build, test and lint commands.
5. First vertical slice: pairing + one `Hotkey` action end to end, test-first in `domain`/`application`.

## Working preferences

- Chat in Spanish; code, identifiers, comments, UI copy and docs in English.
- Conventional Commits, without `Co-Authored-By` or AI attribution lines.
- Clean/hexagonal architecture and testing matter to the user; explain the "why" behind decisions.
