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
- Run: `dotnet run --project src/LeverDeck.App` (listens on `http://0.0.0.0:8765`, `GET /health`)
- Regenerate the TypeScript protocol types after changing `LeverDeck.Protocol`:
  `dotnet run --project tools/LeverDeck.ProtocolGen -- ../web/src/app/core/protocol`
  (`TypeScriptSnapshotTests` fails while the committed files are out of date).
- Publish: `dotnet publish src/LeverDeck.App -c Release -r win-x64 -o ../artifacts/leverdeck` (Native AOT).
  Needs the MSVC build tools; if the linker reports `vswhere.exe` not found, add
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer` to `PATH`.

Web: not scaffolded yet (Phase 5).
