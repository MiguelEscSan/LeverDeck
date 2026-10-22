# LeverDeck

Phone/tablet as a Stream Deck–style control surface for a PC. A .NET 10 agent (`agent/`) serves an Angular PWA (`web/`)
and executes actions. Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before making structural changes.

## Rules

- Respect the layer boundaries. In `agent/`, dependencies point inward: `App → Infrastructure → Application → Domain`,
  enforced by project references. OS, network, file system and ASP.NET types never appear in `Domain` or `Application`.
- Light CQRS: commands return only an ack or an error, queries have no side effects. No buses, mediators or event sourcing.
- The wire contract lives in `agent/src/LeverDeck.Protocol`. TypeScript protocol types are generated from it; never edit
  them by hand.
- `Action` is a closed whitelist (`abstract record` + `sealed record` per action), guarded by an architecture test.
  Never add an action that runs arbitrary commands.
- Windows is the only agent platform in scope. macOS is planned: keep OS-specific code in `Infrastructure` behind ports,
  resolve paths with `Environment.GetFolderPath`, and do not implement macOS adapters until asked.
- Angular: feature-first folders, facades with Signals, container/presentational split, standalone + `OnPush`, `inject()`.
  Components never touch the WebSocket. Features do not import each other; shared code goes through `core/`.
- Test-first for domain and application logic, using fake ports.
- English for code, identifiers, comments, UI copy and docs. Conventional Commits.

## Commands

Agent (run from `agent/`):

- Build: `dotnet build LeverDeck.slnx`
- Test: `dotnet test LeverDeck.slnx`
- Run: `dotnet run --project src/LeverDeck.App` (listens on `http://0.0.0.0:8765`, `GET /health`, and logs the
  desktop editor URL with the admin token). Data lives in `%APPDATA%\LeverDeck\`; override it with
  `LeverDeck__DataDirectory=<absolute path>`. Turn mDNS off with `LeverDeck__Mdns__Enabled=false`.
- Manual input check (Debug build, Development environment, loopback only, so `dotnet run`). The first start writes
  `profiles.json` with a "Default" profile; take a profile id and a button id from it, then:
  `curl -X POST http://localhost:8765/debug/execute -H "content-type: application/json" -d "{\"profileId\":\"<id>\",\"pageIndex\":0,\"buttonId\":\"<id>\"}"`
  runs a stored button and injects real input. Never call it from tests or automation.
- Regenerate the TypeScript protocol types after changing `LeverDeck.Protocol`:
  `dotnet run --project tools/LeverDeck.ProtocolGen -- ../web/src/app/core/protocol`
  (`TypeScriptSnapshotTests` fails while the committed files are out of date).
- Publish: `dotnet publish src/LeverDeck.App -c Release -r win-x64 -o ../artifacts/leverdeck` (Native AOT).
  Needs the MSVC build tools; if the linker reports `vswhere.exe` not found, add
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer` to `PATH`. Build `web/` first, or the exe embeds the
  fallback page instead of the PWA.
- Build with the PWA embedded in one step: `dotnet build src/LeverDeck.App -p:BuildWebUi=true` (runs npm in `web/`).
  Without a web build the agent embeds a fallback page; `-p:RequireWebUi=true` makes that an error (CI does this).

Web (run from `web/`, Node 24):

- Install: `npm ci`
- Lint (includes architecture boundaries): `npm run lint`
- Test (Vitest, single run): `npm test`
- Build (into `dist/leverdeck/browser`, embedded by the agent): `npm run build`
- Dev server: `npm start` (`http://localhost:4200`; no agent behind it)
