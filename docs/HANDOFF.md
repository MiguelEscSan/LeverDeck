# LeverDeck — Session Handoff

Context for a new session picking up LeverDeck. Read this first, then [ARCHITECTURE.md](ARCHITECTURE.md) and [../CLAUDE.md](../CLAUDE.md).

## What we are building

A free, self-hosted alternative to the Elgato Stream Deck: any phone or tablet becomes a control surface for the PC.
Tapping a button on the device triggers an action on the PC (hotkeys, media keys, launching apps, OBS scenes, games).

## Where

- **Local path:** `C:\Projects\leverdeck`
- **Remote:** https://github.com/MiguelEscSan/LeverDeck (branch `main`)
- **Current state:** the MVP works end to end (Phase 6) and Phase 7 is under way; see Next steps.

## How (decided)

- **One executable** `leverdeck.exe` in **.NET 10 (LTS), C#**: ASP.NET Core minimal APIs on Kestrel, raw WebSockets
  (not SignalR). It serves the PWA and executes the actions. Native AOT preferred; self-contained single-file publish
  as fallback if a dependency is not AOT-compatible. System.Text.Json source generators for serialization.
  The PWA build is embedded as embedded resources and served via `ManifestEmbeddedFileProvider`.
- **Desktop app (planned, ROADMAP Phase 8b):** LeverDeck installs and runs like Spotify or Discord. A Velopack setup
  installs it per user (Start menu shortcut, clean uninstall, updates from GitHub Releases); the Start menu opens its
  own window that shows the existing Angular editor through WebView2 (no browser, no token URL, no console:
  `WinExe` in Release); closing the window keeps the agent in the tray for the phones; a second launch shows the
  running instance's window. Logs go to a file under the data directory; every start of the agent deletes all
  existing logs and begins a new one (no previous log is kept), with a size cap within a session. Whether the window
  runs under Native AOT or needs a single-file publish is settled by a spike. A native UI rewrite was rejected: it
  would duplicate the editor, and the phones need the PWA anyway.
- **Frontend:** **Angular** PWA (`@angular/pwa`), standalone, Signals, `OnPush`. The user knows Angular well,
  and the UI will grow beyond the grid (profiles, settings, button editor).
- **Architecture:** hexagonal on both sides. The agent is a solution `agent/LeverDeck.slnx` with one project per layer
  (`LeverDeck.Domain`, `LeverDeck.Application`, `LeverDeck.Protocol`, `LeverDeck.Infrastructure`, `LeverDeck.App`)
  under `src/` and xUnit tests under `tests/`; project references enforce the dependency rule. Angular is feature-first
  (`core`, `pairing`, `deck`, `profiles`, `settings`), each with `domain/application/infrastructure/ui`.
- **Light CQRS:** commands vs queries vs pushed events. No buses, mediators, separate read models or event sourcing.
- **Contract:** defined as C# records in `LeverDeck.Protocol`; TypeScript types generated at build time
  (TypeGen, to confirm at scaffold time), never edited by hand.
- **Connectivity:** HTTP + WebSocket on port `8765`. LAN via a per-PC mDNS name **`leverdeck-<pc>.local`** (connect by name so the
  browser origin survives IP changes; the user cannot configure the router). USB on Android via `adb reverse`.
  Optional `mkcert` certificate for HTTPS over LAN.
- **Pairing:** QR with a one-time code → long-lived device token stored in the browser; the agent keeps only a hash in
  `devices.json`. Pair once per origin (LAN and USB are separate origins).
- **Persistence:** the PC is the source of truth (profiles/devices/settings JSON in the app config dir).
- **Security:** `Action` is a closed whitelist (`abstract record` + one `sealed record` per action); never arbitrary
  commands. An architecture test guards exhaustiveness, since C# does not check it for class hierarchies.
- **Platforms:** Windows agent in scope. macOS agent planned but **not to be implemented yet**; keep OS code behind
  ports in `Infrastructure` and resolve paths with `Environment.GetFolderPath` (known folders). The macOS plan (what each
  adapter becomes, the `.app` bundle, signing, the Accessibility permission, Mac App Store apps as launch targets) is
  ROADMAP Phase 9. Devices: anything with a modern browser.
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
- **Distributing the macOS agent through the Mac App Store:** App Store apps run in the App Sandbox, which forbids
  posting keyboard events to other apps and launching arbitrary apps, the agent's core job. It would ship from GitHub
  Releases instead (ROADMAP Phase 9); Mac App Store apps remain supported as launch targets.

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

Full phased plan: [ROADMAP.md](ROADMAP.md); task logs per phase in `odd/tasks/`.

Done (all on `main`):

- Phases 0–5: agent solution and layers, Native AOT, domain and use cases, wire contract with generated TypeScript,
  Windows adapters (SendInput, DPAPI, mDNS with an own `leverdeck.local` responder), PWA shell embedded in the exe.
- Phase 6 (MVP): pairing (QR + 6-digit code), `/ws` and `/admin/ws`, the phone deck (`/deck`) and the desktop editor
  (`/admin`, opened from the `?admin=` link the agent logs at start-up).
- Phase 7 (`odd/tasks/phase-7-make-it-usable.md`): grid editor in `/admin` (profiles, pages, grid size, key
  label/color/icon/action; the PC edits, phones only read), drag and drop of keys (plus Alt + arrows), actions for
  links, registered apps and Steam/Epic games (installed games listed from the launchers' manifests), a Library with
  Apps and Icons, uploaded key icons (PNG, served at `/api/icons/<id>.png`), and a fix so a key's label follows its
  game or app when the game is typed by id (the label is cleared when no name is known).
- P7-4 and P7-5 in the agent (last commit `c3522c2`): tray icon (`App/Tray`: Open editor, Start with Windows, Exit),
  start with Windows through the per-user `Run` key (`RunKeyStartupRegistration`, only from the published exe), private
  firewall rule for the agent port (`PrivateFirewallRule`, added in the background; without admin it logs the `netsh`
  command), and optional HTTPS on the same port with a `mkcert` certificate in `tls/` (`LeverDeck:Https:Enabled`).
  `LeverDeck:Desktop:Enabled=false` turns the tray and the firewall off (the app tests do).

Not verified yet (the last session could not check these):

- The tray icon on screen. It is created with no error logged; look in the hidden-icons arrow of the taskbar.
- The AOT publish. It fails on this PC with "Platform linker not found": install the Desktop development with C++
  workload, then `dotnet publish src/LeverDeck.App -c Release -r win-x64 -o ../artifacts/leverdeck -p:BuildWebUi=true`
  from `agent/`, with the Visual Studio Installer folder on `PATH` (see CLAUDE.md). Check the trimming and AOT warnings.
- HTTPS end to end: install `mkcert`, run `mkcert -install`, set `LeverDeck__Https__Enabled=true`, install the root CA
  (`mkcert -CAROOT`) on the phone, then pair over `https://leverdeck-<pc>.local:8765` (the agent logs its name at start).
- The firewall rule: run the `netsh` command from the log once as administrator if the agent is not elevated. A
  pre-existing rule named `leverdeck` (UDP, any port) is unrelated and was left alone.

Open:

- Manual checks by the owner on the real PC and phone (pairing over LAN and USB, links, games, apps, uploaded icons,
  the items above).
- OBS scenes: postponed, the owner does not use OBS.
- Owner's earlier note: review the Phase 5 code (web shell, embedding, CI) for a severe finding a review bot reported
  but never showed.
- Phase 7 follow-ups noted in the roadmap: the game name for games typed by id (store lookup), and the elevated-target
  warning (UIPI, see future ideas).
- Phase 8: release `leverdeck.exe` from CI, README.

Local setup notes (Windows): .NET SDK 10.0.402 or newer (`global.json`), Node 24, MSVC build tools for the AOT
publish. If `npm ci` crashes or leaves `web/node_modules` half installed, delete `web/node_modules` and run it again.
`web/package.json` overrides `vite` (pinned by `@angular/build`) for dev-server advisories; `braces` has no fixed
release yet and only reaches the lint config.

## Working preferences

- Chat in Spanish; code, identifiers, comments, UI copy and docs in English.
- Conventional Commits, without `Co-Authored-By` or AI attribution lines.
- Clean/hexagonal architecture and testing matter to the user; explain the "why" behind decisions.
