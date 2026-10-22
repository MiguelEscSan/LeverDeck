# LeverDeck Architecture

LeverDeck turns any phone or tablet into a Stream Deck–style control surface for a PC (Windows first, macOS planned).
A single executable on the PC serves a PWA to the device and executes the actions triggered from it.

```
Device (browser / installed PWA)  ──HTTP──▶  leverdeck.exe  → serves the PWA
                                  ──WS────▶  leverdeck.exe  → commands, queries, events
```

## Goals and constraints

- **Zero cost.** No paid accounts, certificates, app stores or cloud services.
- **One executable.** `leverdeck.exe` embeds the PWA; nothing else needs to be installed on the PC (no Docker, no Node).
- **Local only.** Everything runs on the LAN or over USB. No internet dependency.
- **Pair once.** A device is paired a single time and reconnects automatically afterwards.
- **Secure by default.** Only paired devices can trigger actions, and only from a closed set of actions.

## Platform support

| Platform | Status |
|----------|--------|
| Windows (agent) | In scope |
| macOS (agent) | Planned, **not in scope yet** |
| Any device with a modern browser (PWA) | In scope |

macOS is not implemented for now, but the code must not make it harder later:

- OS-specific code (input injection, app launching, autostart, config paths) lives only in `infrastructure`, behind ports.
- Config paths are resolved with `Environment.GetFolderPath` (known folders), never hardcoded to `%APPDATA%`.
- When macOS is added, it gets its own adapters (Accessibility permission for input, `open -a` for apps, a LaunchAgent for autostart);
  `domain`, `application`, `protocol` and the PWA must not change.

## Components

| Component | Tech | Responsibility |
|-----------|------|----------------|
| `agent/` | .NET 10 (LTS), C# — ASP.NET Core minimal APIs on Kestrel, raw WebSockets | HTTP + WebSocket server, action execution, persistence, discovery |
| `web/` | Angular (standalone, Signals, `@angular/pwa`) | Deck, profiles, settings and pairing UI |
| `agent/src/LeverDeck.Protocol` | C# records + System.Text.Json source generators | Single source of truth for the wire contract; TypeScript types are generated from it |

The agent is published as one executable, `leverdeck.exe`: Native AOT preferred; if a dependency is not AOT-compatible,
the fallback is a self-contained single-file publish. Serialization uses System.Text.Json source generators (required by AOT).
WebSockets are raw ASP.NET Core WebSockets, not SignalR.

The Angular build output (`web/dist/`) is embedded into the executable as embedded resources and served through
`ManifestEmbeddedFileProvider`.

## Connectivity

- **Port:** `8765` (HTTP and WebSocket on the same port).
- **LAN:** the agent advertises itself via mDNS as `leverdeck.local`. Devices always connect by name, never by IP,
  so the browser origin stays stable when the PC's IP changes.
- **USB (Android):** `adb reverse tcp:8765 tcp:8765`. The device connects to `localhost:8765`.
  `localhost` is a secure context, so the full PWA (service worker, Wake Lock) works over USB.
- **HTTPS over LAN (optional):** a `mkcert` certificate issued for `leverdeck.local`, with its CA installed on the device.
  Without it, the LAN version works as a regular web page but is not installable.
- **Firewall:** allow UDP `5353` (mDNS) and TCP `8765` on the **private** network profile only.
- **Fallback:** the pairing QR also carries the current IP in case `.local` does not resolve on a device.

## Pairing and authentication

1. The agent shows a QR code with `http://leverdeck.local:8765/pair?code=<one-time-code>`.
2. The device redeems the one-time code and receives a long-lived **device token**.
3. The PWA stores the token in browser storage. The agent stores only a **hash** of it in `devices.json`.
4. Every WebSocket connection authenticates with the token. Unknown or revoked tokens are rejected.
5. Devices can be revoked from the PC.

Browser storage is per origin: `leverdeck.local:8765` and `localhost:8765` are different origins, so USB and LAN
are paired separately (once each). On iOS the PWA must be added to the home screen, otherwise Safari may evict its storage.

## Persistence

The PC is the source of truth. Profiles, buttons and settings live on the agent, not in the browser,
so every paired device sees the same configuration.

```
%APPDATA%\LeverDeck\
├── profiles.json
├── devices.json     (token hashes, names, pairing dates)
└── settings.json
```

## Agent (.NET) — hexagonal solution

One project per layer in `LeverDeck.sln`, so dependency rules are enforced by project references:

```
agent/
├── LeverDeck.sln
├── src/
│   ├── LeverDeck.Domain/          entities and pure rules: Profile, Button, Action, Device
│   ├── LeverDeck.Application/     commands, queries and ports (interfaces)
│   ├── LeverDeck.Protocol/        wire messages (C# records, System.Text.Json source generators)
│   ├── LeverDeck.Infrastructure/  adapters: Kestrel/WebSockets, mDNS, input injection, OBS, JSON files
│   └── LeverDeck.App/             Program.cs — composition root, wires adapters into use cases
└── tests/                         xUnit test projects
```

**Dependency rule** (arrows point to what a project may reference):

```
App ──▶ Infrastructure ──▶ Application ──▶ Domain
          │                    
          └──▶ Protocol ──▶ Domain
```

- `Domain` depends on nothing but the base class library.
- `Application` depends only on `Domain`. It defines the ports:
  `ActionExecutor`, `ProfileRepository`, `DeviceRepository`, `TokenGenerator`, `EventPublisher`.
- `Domain` and `Application` reference no ASP.NET Core or OS-specific packages.
- `Infrastructure` implements the ports. It is the **only** project that knows about Windows, the network or the file system.
- `App` contains no logic: it builds the adapters, injects them into use cases and starts the server.

### Windows adapters (`Infrastructure`)

| Port concern | Adapter |
|--------------|---------|
| Hotkeys, media keys | `SendInput` through CsWin32 |
| Launch apps | `Process.Start` with `UseShellExecute` |
| Steam/Epic games | Store URLs built from validated ids |
| OBS | obs-websocket client (library to confirm AOT compatibility) |
| mDNS | Native `DnsServiceRegister` (`dnsapi.dll`, Windows 10+) via P/Invoke |
| Persistence | JSON files |
| Config paths | `Environment.GetFolderPath` / known folders |

## Light CQRS

Commands and queries are separated in the application layer. There are no separate read/write models,
no command bus or mediator, and no event sourcing.

- **Commands** change state and return nothing but an acknowledgement or an error
  (e.g. `ExecuteAction`, `SaveProfile`, `PairDevice`, `RevokeDevice`).
- **Queries** read state and have no side effects (e.g. `GetProfiles`, `GetProfile`, `GetDevices`).
- **Events** are pushed by the agent to keep every device in sync (e.g. `ProfileUpdated`, `DeviceRevoked`).
- The full catalogue of domain types, use cases and wire messages lives in [ROADMAP.md](ROADMAP.md) (Phases 2 and 3).
- One class per use case, one public `Execute`/`Handle` method.

The WebSocket protocol mirrors this split: every message is a `Command`, a `Query`, a `QueryResult`,
an `Ack`/`Error`, or an `Event`, correlated by a request id.

## PWA (Angular) — feature-first

```
web/src/app/
├── core/               connection port, generated protocol types, DI tokens
├── pairing/
├── deck/
├── profiles/
└── settings/
    ├── domain/          models and rules of the feature
    ├── application/     facade: Signals for queries, methods for commands
    ├── infrastructure/  adapters implementing core ports
    └── ui/
        ├── pages/       containers — inject the facade
        └── components/  presentational — input()/output() only
```

- Components never touch the WebSocket; they talk to the facade only.
- Ports are abstract classes or `InjectionToken`s; adapters are provided in `app.config.ts`.
- The connection adapter owns reconnection and the USB/LAN fallback; the UI is unaware of the transport.
- Standalone components, `ChangeDetectionStrategy.OnPush`, Signals for state, `inject()` for DI.
- Features do not import from each other; shared code goes through `core/`.
- Layer and feature boundaries are enforced with `eslint-plugin-boundaries`.

## Rules

1. **The contract lives in `LeverDeck.Protocol`.** TypeScript protocol types are generated from its C# records at build time
   (tool: TypeGen, to confirm at scaffold time) and never edited by hand.
   A protocol change that the frontend does not handle must break the frontend build.
2. **Whitelisted actions only.** `Action` is a closed set: an `abstract record Action` with one `sealed record` per action
   (`Hotkey`, `MediaKey`, `LaunchApp`, `OpenUrl`, `ObsScene`, ...). There is no "run arbitrary command" action,
   and action parameters are validated in the domain. C# does not check exhaustiveness over class hierarchies, so an
   architecture test (reflection over all `Action` subtypes) asserts every subtype is handled by the executor and the
   whitelist; the ExhaustiveMatching.Analyzer package may be added as well (to confirm).
3. **Dependencies point inward.** Domain and application code never import framework, OS or network types.
4. **Tests per layer.**
   - Domain and application: unit tests with fake ports, written test-first.
   - Infrastructure: integration tests against real adapters where feasible.
   - Angular: facades and domain tested without TestBed where possible; presentational components tested through their inputs/outputs.
5. **English everywhere** in code, identifiers, comments, UI copy and docs.
6. **Conventional Commits.**
