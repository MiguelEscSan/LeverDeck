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

The Angular build output (`web/dist/leverdeck/browser`) is embedded into the executable as embedded resources and
served through `ManifestEmbeddedFileProvider` (`LeverDeck.App/WebUi.targets` and `WebUi.cs`):

- Static files come from the embedded manifest. Any other GET that is not an agent route (`/api`, `/ws`, `/admin/ws`,
  `/debug`, `/health`) gets `index.html`, so client routes survive a reload; other methods get 404.
- `Cache-Control`: hashed bundles (`main-7W3HWPXS.js`) are `public, max-age=31536000, immutable`; everything else
  (`index.html`, `ngsw.json`, `ngsw-worker.js`, the manifest, icons) is `no-cache`, so the service worker sees updates.
- Without a web build (fresh clone, agent-only work) the agent still builds and embeds a small fallback page that says
  the web app was not built. `dotnet build` never runs npm; `-p:BuildWebUi=true` runs `npm ci` and `npm run build`
  first, and `-p:RequireWebUi=true` (used by CI and release) fails instead of falling back. The embedded source is
  recorded as `[AssemblyMetadata("LeverDeck.WebUi", "angular" | "fallback")]`.

## Connectivity

- **Port:** `8765` (HTTP and WebSocket on the same port).
- **LAN:** the agent advertises itself via mDNS under a name of its own, `leverdeck-<pc>.local`, so several PCs can
  run LeverDeck on the same network. The name comes from the computer name (lowercase, accents dropped, anything but
  `a-z`/`0-9` turned into `-`, at most 63 characters, a random suffix when nothing is left), is chosen once and kept in
  `hostname` in the data directory, so renaming the PC does not break paired devices; `LeverDeck__Mdns__HostName`
  overrides it (a label or `label.local`). Devices always connect by name, never by IP, so the browser origin stays
  stable when the PC's IP changes. Each PC is its own origin on the phone: its own pairing, token and PWA install.
- **mDNS responder:** Windows publishes the `DnsServiceRegister` service records but does not answer A queries for
  their target host, so the agent answers queries for its name itself (RFC 6762, BCL sockets only). It binds
  UDP `5353` with `SO_REUSEADDR` and without exclusive use, so it shares the port with Windows' own listener
  (Dnscache) and other responders, joins `224.0.0.251` on the LAN interface chosen by `LanAddressSelector`, sends
  with IP TTL 255, and keeps multicast loopback on (in Winsock it governs receiving, so the PC's own resolver queries
  reach it). Records: A = the LAN IPv4 address, TTL 120 s, cache-flush; every other type for the name gets an NSEC
  stating A is the only type (§6.1), so AAAA lookups fail fast; each answer carries the other record as additional.
  QU questions get a unicast answer only if the record was multicast in the last 30 s and the querier is not this PC
  (§5.4); legacy resolvers (source port other than 5353) get a unicast answer with their query ID, the question and
  TTL 10 s. Responses, malformed packets, non-zero opcodes or response codes and other names are ignored. It sends
  two announcements one second apart on start and on address change, and a goodbye (TTL 0) on stop. No probing or
  known-answer suppression; another host answering for the name with a different address is logged as a warning
  that names `LeverDeck__Mdns__HostName`, the way to give this PC another name.
- **USB (Android):** `adb reverse tcp:8765 tcp:8765`. The device connects to `localhost:8765`.
  `localhost` is a secure context, so the full PWA (service worker, Wake Lock) works over USB.
- **HTTPS over LAN (optional):** a `mkcert` certificate issued for the PC's mDNS name, with its CA installed on the device.
  Without it, the LAN version works as a regular web page but is not installable.
- **Firewall:** allow UDP `5353` (mDNS) and TCP `8765` on the **private** network profile only.
- **Fallback:** the pairing QR also carries the current IP in case `.local` does not resolve on a device.

## Pairing and authentication

1. The agent shows a QR code with `http://leverdeck-<pc>.local:8765/pair?code=<one-time-code>`.
2. The device redeems the one-time code and receives a long-lived **device token**.
3. The PWA stores the token in browser storage. The agent stores only a **hash** of it in `devices.json`.
4. Every WebSocket connection authenticates with the token. Unknown or revoked tokens are rejected.
5. Devices can be revoked from the PC.

Browser storage is per origin: `leverdeck-<pc>.local:8765` and `localhost:8765` are different origins, so USB and LAN
are paired separately (once each). On iOS the PWA must be added to the home screen, otherwise Safari may evict its storage.

### Desktop editor (admin)

The editor on the PC uses `GET /admin/ws`, the only channel that accepts admin-scoped commands and queries.

- **Localhost is not trusted.** With `adb reverse tcp:8765 tcp:8765` a phone reaches the agent from loopback too, so
  "the connection comes from 127.0.0.1" proves nothing about who is on the other side.
- **Persistent admin token.** The agent generates one high-entropy admin token the first time it runs and stores it
  under `%APPDATA%\LeverDeck\` (resolved with `Environment.GetFolderPath`), protected with DPAPI when that is
  AOT-compatible, otherwise as a plain file in the user profile. It is reused across restarts, so a bookmarked editor
  keeps working.
- **`/admin/ws` requires it.** A connection without the right token (compared in constant time) is rejected before
  any message is read.
- **The editor URL carries it.** The editor window (below) opens `http://localhost:8765/?admin=<token>`; the editor
  keeps the token for its connection and moves it out of the URL, so the owner never sees it. Anyone who can read the
  user's profile folder can already control the PC, so the token adds no new exposure there.

### Editor window (desktop app)

LeverDeck shows the same Angular editor in a window of its own (`LeverDeck.App/Desktop/EditorWindow.cs`), through
WebView2, so the owner needs no browser and no URL. Windows-only code in `App`, like the tray.

- **Native AOT.** The few WebView2 COM interfaces are declared with source-generated COM (`[GeneratedComInterface]`,
  `[GeneratedComClass]`) in `Desktop/WebView2.cs`, with IIDs and vtable order from `WebView2.h` in the
  `Microsoft.Web.WebView2` package; each declares only the vtable prefix it uses, with `Unused*` placeholder slots.
  The package is referenced with no assets (its managed wrappers need built-in COM); the AOT build links
  `WebView2LoaderStatic.lib` (`DirectPInvoke`), so the exe stays one file, and JIT builds load `WebView2Loader.dll`
  from the output folder.
- **One UI thread.** The tray thread is STA and owns both the tray window and the editor window with its web view;
  Kestrel stays on the thread pool. The process is per-monitor DPI aware (v2), set once before any window; the web
  view's bounds follow `WM_SIZE` and `WM_DPICHANGED`.
- **Close hides.** Closing the window hides it (the web view stays loaded but invisible) and the agent keeps serving
  the phones from the tray; "Open LeverDeck" in the tray menu, a left click on the icon or a second launch shows it
  again. The window's size, position and maximized state are kept in `window.json` in the data directory. On exit the
  web view's controller is closed before the window is destroyed.
- **Profile folder.** The web view's user data always goes to `<DataDirectory>/webview2`; the default folder, next
  to the exe, would be replaced by every installer update.
- **Missing runtime.** WebView2 comes with Windows 11 and Windows 10 updates. When
  `CreateCoreWebView2EnvironmentWithOptions` fails, the window hides again and a message box says so; for a missing
  runtime it offers Microsoft's WebView2 download page. The tray's "Open in browser" still opens the editor in the
  default browser.

### Transport endpoints

All live in `Infrastructure/Transport`, as plain request delegates with source-generated JSON (Native AOT safe).

| Endpoint | Auth | Behaviour |
|---|---|---|
| `POST /api/pairing/redeem` | none (public scope) | JSON only (`415` otherwise), body ≤ 4 KiB (`413`). `200 PairDeviceResponse` with `Cache-Control: no-store`; `400 ErrorDto` for `pairingCodeInvalid`/`pairingCodeExpired`/malformed bodies, `422` for `validationFailed` (device name), `500 internal`. The token is never logged. |
| `GET /api/icons/<id>.png` | none | An uploaded icon (`custom-` + 32 hex digits), `image/png`, cached a year as `immutable` (an id's image never changes), `nosniff`; anything else is `404`. Public because an `<img>` cannot send a token and an icon is not a secret. |
| `GET /ws` | device token in the first message | The first message must be `connectDevice` within 10 s (`LeverDeck:Transport:ConnectTimeout`). On success: `ack`, then the socket is bound to that device. Messages ≤ 64 KiB. |
| `GET /admin/ws` | admin token as `?token=` | Checked in constant time before the upgrade: a wrong or missing token gets `401` and no socket (browsers cannot set headers on a WebSocket). Bound to the admin scope. Messages ≤ 1 MiB (whole profiles). |

Both sockets then answer one message at a time: every `command` gets an `ack` or `error`, every `query` a
`queryResult` or `error`, with the same `requestId`. Scope is enforced by the use cases, so a device sending an admin
message gets `forbidden`. Malformed JSON, unknown types, binary frames and non-request kinds get
`error validationFailed` (with the `requestId` when it can still be read) and the socket stays open. Sends are
serialized per connection; the server pings every 15 s and drops a socket that does not answer within 15 s.

**Close codes.** The agent sends an `error` (or the event) first, then closes; the close reason is the wire error code,
so a client can tell "pair again" from "reconnect" even if it missed the message. After the close frame the client has
2 s to answer before the socket is aborted.

| Code | Reason | When |
|---|---|---|
| `1008` policy violation | `unauthorized` | No `connectDevice` in time, another first message, malformed first message, unknown token |
| `1008` policy violation | `deviceRevoked` | Connecting with a revoked token, or the device was revoked while connected (after the `deviceRevoked` event) |
| `1008` policy violation | `protocolMismatch` | `connectDevice` with another `protocolVersion` |
| `1009` message too big | `validationFailed` | A message over the channel's limit |
| `1011` internal error | `internal` | Authentication failed unexpectedly |
| `1001` going away | `shutdown` | The agent is stopping |
| `1000` normal | — | Answer to a close started by the client |

**Event routing.** `EventFanOut` subscribes to the in-memory broadcaster and loads payloads through the query use
cases (as the admin), so a query result and an event never disagree.

| Application event | Pushed as | To |
|---|---|---|
| `ProfileUpdated` | `profileUpdated { profile }` | every connection |
| `ProfileDeleted` | `profileDeleted { profileId }` | every connection |
| `DevicesChanged`, and a device socket connecting or disconnecting | `devicesChanged { devices }` (with `connected` from the connection registry) | admin connections |
| `DeviceRevoked` | `deviceRevoked { deviceId }`, then close `1008 deviceRevoked` | the revoked device's connections |
| `PairingSessionChanged` | `pairingSessionChanged { session }` (with `qrUrl` and `fallbackUrl`) | admin connections |
| `SettingsUpdated` | `settingsUpdated { settings }` | admin connections |
| `IconsChanged` | `iconsChanged { icons }` | admin connections |

Events published while a command runs reach the clients before that command's `ack`. `qrUrl` is
`http://leverdeck-<pc>.local:8765/pair?code=<code>`; `fallbackUrl` uses the LAN IPv4 address chosen as for mDNS, or is
`null` when there is none.

## Persistence

The PC is the source of truth. Profiles, buttons and settings live on the agent, not in the browser,
so every paired device sees the same configuration.

```
%APPDATA%\LeverDeck\
├── profiles.json
├── devices.json     (token hashes, names, pairing dates)
├── settings.json
├── icons/           (uploaded key icons, <custom-id>.png; see Transport endpoints)
├── logs/            (Release only: leverdeck.log of the current run; see Logs)
├── window.json      (the editor window's size and position)
├── webview2/        (the editor window's WebView2 profile)
└── admin-token      (generated once, DPAPI-protected; see Desktop editor)
```

- Each file has its own persistence records with a `schemaVersion` (not the wire DTOs: `devices.json` keeps token
  hashes, which never go on the wire) and its own source-generated `JsonSerializerContext`.
- Writes are atomic (temp file, flush, rename) and serialized by one lock per file. A missing file is a first run:
  no devices, default settings, and a seeded "Default" profile that is written at once so its id stays stable.
- A file that cannot be parsed, has an unknown schema version or breaks a domain rule throws and is left untouched;
  user data is never silently replaced by defaults.
- Secrets at rest (the admin token, the OBS password) are protected with DPAPI for the current Windows user.
- The folder is `LeverDeck:DataDirectory` when set (tests use a temp folder), else
  `Environment.GetFolderPath(ApplicationData)\LeverDeck`.

## Logs

Release builds are `WinExe` (no console window), so they log to `<DataDirectory>/logs/leverdeck.log` through
`FileLoggerProvider` (`Infrastructure/Logging`, no reflection, AOT safe); Debug builds keep the console, so
`dotnet run` still prints everything. Levels come from the usual `Logging` configuration.

- **Every start of the agent begins a new log.** `SessionLogFile.Start` deletes every `*.log` in the folder and opens
  an empty `leverdeck.log`; no previous log is kept (the owner's rule). A start is a new process: Windows sign-in,
  or quitting from the tray and opening LeverDeck again. Closing or reopening the editor window is not a start.
  A file that cannot be deleted is left for the next start.
- **10 MB cap per run.** A tray session can last weeks, so the first line that would cross the cap is replaced by a
  notice ("The log reached its 10 MB limit; later messages are dropped until LeverDeck restarts.") and nothing more is
  written. Stopping rather than truncating keeps the start of the session, where most problems show.
- Each line is `<UTC time> <level> <category>: <message>`, followed by the exception if any; every write is flushed.
  The file is opened with `FileShare.Read`, so it can be read while the agent runs.
- **The admin token is never logged in Release.** The log lives next to the DPAPI-protected token; only Debug builds
  print the editor URL with the token (for `dotnet run`).

## Agent (.NET) — hexagonal solution

One project per layer in `LeverDeck.slnx`, so dependency rules are enforced by project references:

```
agent/
├── LeverDeck.slnx
├── src/
│   ├── LeverDeck.Domain/          entities and pure rules: Profile, Button, Action, Device
│   ├── LeverDeck.Application/     commands, queries and ports (interfaces)
│   ├── LeverDeck.Protocol/        wire messages (C# records, System.Text.Json source generators)
│   ├── LeverDeck.Infrastructure/  adapters: Kestrel/WebSockets, mDNS, input injection, OBS, JSON files
│   └── LeverDeck.App/             Program.cs — composition root, wires adapters into use cases
├── tools/
│   └── LeverDeck.ProtocolGen/     generates web/src/app/core/protocol/*.ts from LeverDeck.Protocol
└── tests/                         xUnit: Domain, Application, Protocol (contract), Infrastructure (mappers),
                                   Architecture (dependency rule), App (endpoints)
```

**Dependency rule** (arrows point to what a project may reference):

```
App ──▶ Infrastructure ──▶ Application ──▶ Domain
          │
          └──▶ Protocol (references nothing: wire DTOs only)
```

- `Domain` depends on nothing but the base class library.
- `Application` depends only on `Domain`. It defines the ports:
  `ActionExecutor`, `ProfileRepository`, `DeviceRepository`, `TokenGenerator`, `EventPublisher`.
- Application is organized by entity (`Profiles/`, `Devices/`, `Pairing/`, `Settings/`, `Actions/`), like Domain.
  Each entity folder owns its ports in a `Ports/` subfolder; cross-cutting ports (`IClock`, `IEventPublisher`,
  `IServiceAdvertiser`) live in `Common/Ports/`. A use case may depend on another entity's port, never on another
  use case.
- `Domain` and `Application` reference no ASP.NET Core or OS-specific packages.
- `Protocol` holds wire DTOs only and references no other project; domain types never go on the wire.
  `Infrastructure/ProtocolMapping` maps DTOs to and from the domain, and every invalid DTO becomes a
  `validationFailed` result instead of an exception.
- `Infrastructure` implements the ports. It is the **only** project that knows about Windows, the network or the file system.
- `App` contains no logic: it builds the adapters, injects them into use cases and starts the server.

### Windows adapters (`Infrastructure`)

`Infrastructure`, `App` and the test projects that reference them target `net10.0-windows`; `Domain`, `Application`
and `Protocol` stay `net10.0`. CsWin32 generates the interop for x64 (`CsWin32PlatformTarget`), the only published
runtime (`win-x64`).

| Port concern | Adapter |
|--------------|---------|
| Running actions | `AgentActionExecutor`: hotkeys and media keys through `SendInputActionExecutor` (`SendInput` via CsWin32); links, store games and registered apps through the Windows shell; `ActionExecutorCoverage` states how every action type is routed |
| Launch apps | `Process.Start` with `UseShellExecute`, the executable path read from the settings (never from a client), no arguments |
| Open URL | `Process.Start` with `UseShellExecute` on the validated `http`/`https` URL (default browser) |
| Steam/Epic games | Store URIs built from validated ids: `steam://rungameid/<id>`, `com.epicgames.launcher://apps/<AppName>?action=launch&silent=true` |
| Installed games (`IGameLibrary`) | `InstalledGameLibrary`: Steam's `libraryfolders.vdf` and `appmanifest_*.acf` (folder from the `SteamPath` registry value, else Program Files (x86)), and the Epic launcher's `*.item` manifests under ProgramData |
| OBS | obs-websocket client (library to confirm AOT compatibility) |
| mDNS | `MdnsHostNameResponder` (UDP 5353 responder for the A record of the PC's `leverdeck-<pc>.local` name, see Connectivity) plus native `DnsServiceRegister` (`dnsapi.dll`, Windows 10+) via CsWin32 for the service records, combined by `CompositeServiceAdvertiser` and run as a hosted service |
| Persistence | JSON files (see Persistence) |
| Secrets at rest | DPAPI (`ProtectedData`, current user) |
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
├── app.ts, app.config.ts, app.routes.ts   composition root: providers (ports → adapters) and routes
├── core/
│   ├── protocol/        generated wire types (never edited by hand)
│   ├── auth/            device and admin tokens (Web Storage), pairing redeem API, route guards
│   ├── keys/            the bundled key icon set, shared by the deck and the editor
│   ├── theme/           the System / Dark / Light preference of this device (`localStorage`), applied to the document
│   └── connection/      shared module, same layers as a feature (no ui/)
│       ├── domain/          ConnectionStatus, message unions, backoff policy, rejections, request errors
│       ├── application/     ConnectionPort (abstract class), RequestChannel, ConnectionFacade (`/ws`) and
│       │                    AdminConnectionFacade (`/admin/ws`)
│       └── infrastructure/  WebSocket adapter, wire format, socket URLs, provideConnection()
└── features/
    ├── shell/           app frame: header with the name and the connection status
    ├── pairing/         `/pair` and `/pair?code=…`: type or receive the code, redeem it, keep the device token
    ├── deck/            `/deck`: the active profile's grid; a tap sends `executeAction`
    ├── admin/           `/admin`: the desktop editor (profiles, grid and key editing, pairing, devices with revoke);
    │                    the only place layouts change
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
- Layer and feature boundaries are enforced with `eslint-plugin-boundaries` (`web/eslint.config.js`): a layer may
  import only inner layers of its own feature and `core/`; presentational components may import only domain types and
  other components; files outside `core/`, `features/` and the composition root fail the lint.
- Theme tokens are CSS custom properties (`--ld-*`) in `web/src/styles.scss`: dark first, light via
  `prefers-color-scheme`, with the mobile baseline (safe areas, `100dvh`, no tap highlight, 16 px inputs). Each device
  may pin a theme (`ThemeFacade` in `core/theme`, per device, never synced from the agent): `data-theme="dark|light"`
  on `<html>` beats the media query, and an inline script in `index.html` applies it before the first paint.

## Rules

1. **The contract lives in `LeverDeck.Protocol`.** TypeScript protocol types are generated from its serializer contract by
   `agent/tools/LeverDeck.ProtocolGen` (TypeGen was evaluated and rejected: it cannot emit discriminated unions) and
   never edited by hand; a test fails when the committed files are out of date.
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
