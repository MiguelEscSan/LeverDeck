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

- **One executable** `leverdeck.exe` in **.NET 10 (LTS), C#**: ASP.NET Core minimal APIs on Kestrel, raw WebSockets
  (not SignalR). It serves the PWA and executes the actions. Native AOT preferred; self-contained single-file publish
  as fallback if a dependency is not AOT-compatible. System.Text.Json source generators for serialization.
  The PWA build is embedded as embedded resources and served via `ManifestEmbeddedFileProvider`.
- **Frontend:** **Angular** PWA (`@angular/pwa`), standalone, Signals, `OnPush`. The user knows Angular well,
  and the UI will grow beyond the grid (profiles, settings, button editor).
- **Architecture:** hexagonal on both sides. The agent is a solution `agent/LeverDeck.slnx` with one project per layer
  (`LeverDeck.Domain`, `LeverDeck.Application`, `LeverDeck.Protocol`, `LeverDeck.Infrastructure`, `LeverDeck.App`)
  under `src/` and xUnit tests under `tests/`; project references enforce the dependency rule. Angular is feature-first
  (`core`, `pairing`, `deck`, `profiles`, `settings`), each with `domain/application/infrastructure/ui`.
- **Light CQRS:** commands vs queries vs pushed events. No buses, mediators, separate read models or event sourcing.
- **Contract:** defined as C# records in `LeverDeck.Protocol`; TypeScript types generated at build time
  (TypeGen, to confirm at scaffold time), never edited by hand.
- **Connectivity:** HTTP + WebSocket on port `8765`. LAN via mDNS name **`leverdeck.local`** (connect by name so the
  browser origin survives IP changes; the user cannot configure the router). USB on Android via `adb reverse`.
  Optional `mkcert` certificate for HTTPS over LAN.
- **Pairing:** QR with a one-time code → long-lived device token stored in the browser; the agent keeps only a hash in
  `devices.json`. Pair once per origin (LAN and USB are separate origins).
- **Persistence:** the PC is the source of truth (profiles/devices/settings JSON in the app config dir).
- **Security:** `Action` is a closed whitelist (`abstract record` + one `sealed record` per action); never arbitrary
  commands. An architecture test guards exhaustiveness, since C# does not check it for class hierarchies.
- **Platforms:** Windows agent in scope. macOS agent planned but **not to be implemented yet**; keep OS code behind
  ports in `Infrastructure` and resolve paths with `Environment.GetFolderPath` (known folders). Devices: anything with a
  modern browser.
- **Cost:** zero. No app stores, no paid certificates, no cloud.

## Rejected (and why)

- **Docker** to host the PWA: containers cannot inject input into the Windows host, and Docker Desktop + WSL2 is far too heavy.
- **Rust agent:** the original choice (axum + tokio, `ts-rs` for types). Replaced by .NET because the owner's main
  stack is C#/.NET (maintainability and portfolio fit). Accepted trade-offs: a bigger executable (~15–30 MB vs ~5–10 MB)
  and higher idle memory.
- **WASM frontend** (Leptos, Yew): slower builds, bigger bundle, smaller ecosystem; type sharing is covered by generated
  TypeScript types.
- **Native mobile app:** iOS needs a Mac and a paid account to avoid 7-day re-signing; a PWA works everywhere for free.
- **Full CQRS:** single user, JSON storage — no benefit.
- **Fixing IP by MAC / router reservation:** the browser cannot see MACs, and the user has no router access. mDNS name instead.
- **Static IP on the PC:** risk of conflicts on a network the user does not control.

## Future ideas (not scheduled)

- **Launch Steam/Epic games:** `steam://rungameid/<appid>` and `com.epicgames.launcher://apps/<AppName>?action=launch&silent=true`
  (Epic URL format still to be verified). Installed games can be listed from Steam's `libraryfolders.vdf` / `appmanifest_*.acf`
  and Epic's `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item`. Model as a `LaunchGame(Store, Id)` action record plus a
  `GameLibrary` port; build the URL from a validated id, never accept a free-form URL.
- **OBS integration** through OBS's built-in WebSocket server (v28+).
- **Games that ignore simulated input:** some anti-cheat systems reject keystrokes flagged as injected by `SendInput`
  (`LLKHF_INJECTED`), so hotkeys may not reach those games. A kernel driver is out of scope. Revisit in a later iteration:
  document known affected games and prefer non-input actions (store launch URLs, OBS) where possible.
- **Elevated targets (UIPI):** a non-elevated agent cannot send input to an app running as administrator. Decide later
  whether to detect this and warn the user, or offer an opt-in elevated mode.

## Next steps

Full phased plan: [ROADMAP.md](ROADMAP.md).

Phases 0 and 1 are done: the agent solution is scaffolded, the dependency rule is tested, and the Native AOT spike
passed (~9.5 MB `leverdeck.exe`, no AOT warnings). Phase 2 is done: the whole domain and all 18 use cases, test-first
against fakes (see `odd/tasks/phase-2-domain-core.md`). Next is Phase 3: wire contract and TypeScript generation, and
the admin authentication decision.

## Working preferences

- Chat in Spanish; code, identifiers, comments, UI copy and docs in English.
- Conventional Commits, without `Co-Authored-By` or AI attribution lines.
- Clean/hexagonal architecture and testing matter to the user; explain the "why" behind decisions.
