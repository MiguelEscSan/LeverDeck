# LeverDeck Roadmap

How to go from docs-only to a working app. Each phase ends in something runnable and committed; do not start a phase
until the previous one's **Done when** holds. Read [ARCHITECTURE.md](ARCHITECTURE.md) first; this plan only orders the
work.

Guiding rule: build **one thin vertical slice end to end** (phone tap → hotkey on the PC) before widening anything.

## Phase 0 — Tooling and repo hygiene

- [x] `.gitattributes` with LF line endings.
- [x] Install the .NET 10 SDK and the "Desktop development with C++" build tools (needed for Native AOT on Windows).
      Node LTS is installed; the Angular CLI comes in Phase 5.
- [x] Add `.editorconfig` and a root `.gitignore` (`bin/`, `obj/`, `node_modules/`, `dist/`, `.atl/*.cache.json`).
      TypeScript conventions were added with the Angular app in Phase 5.
- [x] Add a GitHub Actions workflow on `windows-latest` that builds, tests and AOT-publishes `agent/`.
      The `web/` job was added in Phase 5.

**Done when:** a clean clone builds in CI with empty projects.

## Phase 1 — Agent skeleton (.NET)

- [x] Create `agent/LeverDeck.slnx` with `src/LeverDeck.Domain`, `Application`, `Protocol`, `Infrastructure`, `App`,
      sharing `Directory.Build.props` (nullable, warnings as errors) and central package versions.
- [x] Wire the dependency rule as project references only (`App → Infrastructure → Application → Domain`,
      `Infrastructure → Protocol`). Domain and Application reference no packages.
- [x] Create `tests/LeverDeck.Domain.Tests`, `Application.Tests`, `Architecture.Tests` and `App.Tests` (xUnit v2).
- [x] Architecture test: reads the `.csproj` files and asserts the allowed project references and that Domain and
      Application have no package references (verified to fail when the rule is broken).
- [x] `App`: minimal API (`CreateSlimBuilder`) on port `8765` with `GET /health` returning `ok`.
- [x] **AOT spike:** `dotnet publish src/LeverDeck.App -c Release -r win-x64` with `PublishAot=true` produces
      `leverdeck.exe` of **~9.5 MB** with **no trim/AOT warnings**, and it answers `/health`. Native AOT stays.

**Done when:** `dotnet test` is green and the published exe answers `/health`.

## Phase 2 — Domain and application core (test-first)

Model the **whole** domain and every use case now, even the ones whose adapters arrive in Phase 7. Domain types and use
cases are cheap to write against fakes, and having them complete keeps the contract in Phase 3 stable.

### 2.1 Domain (`LeverDeck.Domain`)

**Profiles and layout**

| Type | Kind | Content and invariants |
|---|---|---|
| `Profile` | Aggregate root | `ProfileId`, `ProfileName`, `KeyColor` (sidebar dot), `GridSize`, `Page[]`. At least one page. Name 1–40 chars, unique across profiles. |
| `Page` | Entity | `PageIndex` (0-based, contiguous), `Button[]`. Max 10 pages per profile. |
| `Button` | Entity | `ButtonId`, `GridPosition`, `ButtonAppearance`, `Action`. One button per position per page. |
| `GridSize` | Value object | `Columns` × `Rows`, allowed: 3×3, 5×3, 8×4. Shrinking is rejected if a button falls outside. |
| `GridPosition` | Value object | `Row`, `Column`, both inside the profile's `GridSize`. |
| `ButtonAppearance` | Value object | `Label` (0–12 chars), `KeyColor`, `IconId`. |
| `KeyColor` | Enum | Closed palette from VISUAL-REFERENCE.md: `Red`, `Blue`, `Orange`, `Purple`, `Teal`, `Dark`. |
| `IconId` | Value object | Id from the bundled icon set; unknown ids are rejected. |

**Actions** — closed hierarchy, `abstract record Action`, one `sealed record` per action. Parameters are validated
on construction; nothing is a free-form command.

| Action | Parameters and validation | Executed by | Adapter phase |
|---|---|---|---|
| `Hotkey` | `Modifiers` (Ctrl, Shift, Alt, Win) + one `VirtualKey` from an allow-list | Agent | 4 |
| `MediaKey` | `PlayPause`, `Next`, `Previous`, `Stop`, `VolumeUp`, `VolumeDown`, `Mute` | Agent | 4 |
| `LaunchApp` | `AppId` pointing to an app registered on the PC (path chosen in the desktop UI, never sent by a phone); no arguments | Agent | 7 |
| `OpenUrl` | Absolute `http`/`https` URL only | Agent | 7 |
| `ObsScene` | `SceneName` (1–100 chars) | Agent | 7 |
| `LaunchGame` | `GameStore` (`Steam`, `Epic`) + validated `GameId` (Steam: digits; Epic: `[A-Za-z0-9]+`) | Agent | 7 |
| `ChangePage` | `Next`, `Previous` or a `PageIndex` | PWA (navigation) | 6 |
| `SwitchProfile` | `ProfileId` | PWA (navigation) | 7 |

**Devices and pairing**

| Type | Kind | Content and invariants |
|---|---|---|
| `Device` | Aggregate root | `DeviceId`, `DeviceName` (1–40 chars), `TokenHash`, `PairedAt`, `LastSeenAt`, `RevokedAt?`. A revoked device can never authenticate again. |
| `TokenHash` | Value object | Hash of the device token; the raw token is never stored. |
| `PairingSession` | Entity | `PairingCode` (6 digits), `ExpiresAt` (5 minutes), `RedeemedAt?`. Single use; only one active session at a time — starting a new one cancels the previous. |

**Settings**

| Type | Kind | Content |
|---|---|---|
| `AgentSettings` | Aggregate root | `StartWithWindows`, `DefaultGridSize`, `ObsConnection?` (host, port, password reference), `RegisteredApp[]` (`AppId`, display name, executable path) |

**Domain errors** (returned, not thrown across layers): `ValidationFailed`, `ProfileNotFound`, `DuplicateProfileName`,
`LastProfileCannotBeDeleted`, `PositionOccupied`, `PositionOutOfGrid`, `GridTooSmall`, `PairingCodeInvalid`,
`PairingCodeExpired`, `DeviceNotFound`, `DeviceRevoked`, `ButtonNotFound`, `ActionNotExecutable`.

### 2.2 Ports (`LeverDeck.Application`)

| Port | Responsibility |
|---|---|
| `ActionExecutor` | Executes an agent-side `Action`; returns success or `ActionFailed` |
| `ProfileRepository` | Load, list, save and delete profiles |
| `DeviceRepository` | Load, list and save devices; find by token hash |
| `SettingsRepository` | Load and save `AgentSettings` |
| `PairingSessionStore` | Holds the single active pairing session (in memory) |
| `TokenGenerator` | Generates device tokens and pairing codes (cryptographically random) |
| `TokenHasher` | Hashes and verifies device tokens |
| `Clock` | Current UTC time |
| `EventPublisher` | Pushes domain events to connected clients |
| `ServiceAdvertiser` | Advertises / withdraws `leverdeck.local` |

### 2.3 Use cases

Commands return only `Ack` or an error. The single documented exception is `PairDevice`, whose ack carries the newly
issued device token, because it can be delivered only once. **Scope** says who may call it: `device` = any paired device,
`admin` = the desktop editor on the PC.

**Commands**

| Command | Scope | Input | Behaviour | Events |
|---|---|---|---|---|
| `ConnectDevice` | public | device token, protocol version | Verifies the token hash, rejects revoked devices and protocol mismatches, updates `LastSeenAt` | `DevicesChanged` |
| `ExecuteAction` | device | `ProfileId`, `PageIndex`, `ButtonId` | Resolves the action **from the stored profile** (the client never sends an action) and runs it through `ActionExecutor` | — |
| `SaveProfile` | admin | full profile | Creates or replaces a profile after validating every invariant | `ProfileUpdated` |
| `DeleteProfile` | admin | `ProfileId` | Deletes it; rejects deleting the last profile | `ProfileDeleted` |
| `StartPairing` | admin | — | Creates a new `PairingSession`, cancelling any previous one | `PairingSessionChanged` |
| `CancelPairing` | admin | — | Ends the active session | `PairingSessionChanged` |
| `PairDevice` | public | pairing code, device name | Redeems the code, creates the `Device`, stores the token hash; ack returns the raw token once | `DevicesChanged`, `PairingSessionChanged` |
| `RenameDevice` | admin | `DeviceId`, name | Renames a device | `DevicesChanged` |
| `RevokeDevice` | admin | `DeviceId` | Marks it revoked and closes its connection | `DeviceRevoked`, `DevicesChanged` |
| `UpdateSettings` | admin | `AgentSettings` | Validates and saves settings | `SettingsUpdated` |
| `RegisterApp` / `UnregisterApp` | admin | display name + path / `AppId` | Manages the apps that `LaunchApp` can reference; unregistering an app in use is rejected | `SettingsUpdated` |
| `SaveIcon` / `DeleteIcon` | admin | new `custom-` icon id + PNG (≤ 256 KiB, ≤ 512 px) / icon id | Stores an uploaded key icon once per id; deleting an icon a key shows is rejected (Phase 7) | `IconsChanged` |

**Queries** (no side effects)

| Query | Scope | Returns |
|---|---|---|
| `GetProfiles` | device | Profile summaries: id, name, color |
| `GetProfile` | device | One full profile |
| `GetDevices` | admin | Devices with name, paired/last-seen dates, connected and revoked state |
| `GetPairingSession` | admin | Active code, QR URL (with IP fallback) and expiry, or none |
| `GetSettings` | admin | `AgentSettings` |
| `GetAgentStatus` | device | Agent version, protocol version, OBS connection state |
| `GetIcons` | admin | The uploaded icon ids (Phase 7) |
| `GetInstalledGames` | admin | Steam and Epic games installed on the PC (store, id, name), through the `IGameLibrary` port (Phase 7) |

**Events** pushed by the agent: `ProfileUpdated`, `ProfileDeleted`, `DevicesChanged`, `DeviceRevoked`,
`PairingSessionChanged`, `SettingsUpdated`, `ObsSceneChanged` (Phase 7).

### 2.4 Tasks

- [x] Domain types and invariants above, each with unit tests written first.
- [x] Architecture test: every `Action` subtype is sealed and present in the whitelist (`ActionCatalog`).
      The "handled by the executor" half moves to Phase 4, when the Windows executor exists.
- [x] Ports as interfaces in Application, plus in-memory fakes in the test projects.
- [x] Every command and query as one class with one `ExecuteAsync` method, RED → GREEN against the fakes.
- [x] Tests for the security rules: revoked device rejected, expired/reused/brute-forced (5 wrong guesses) code
      rejected, `ExecuteAction` ignores any action not stored in the profile, admin use cases unreachable from device
      scope.

Open points carried to Phase 3: device-scoped use cases rely on the transport closing a revoked device's connection
(no re-check per call); there is no `ActionFailed` code yet (executor failures pass through); queries return domain
aggregates, mapped to DTOs in Phase 3.

**Done when:** every use case in 2.3 passes against fakes; no adapter code yet.

## Phase 3 — Wire contract and TypeScript generation

`LeverDeck.Protocol` holds DTOs only (C# records, camelCase JSON, System.Text.Json source generation). It maps to and
from the domain in `Infrastructure`; domain types never go on the wire.

### 3.1 Transport

| Channel | Used for |
|---|---|
| `POST /api/pairing/redeem` (HTTP, no auth) | `PairDevice` — the only call made before the device has a token |
| `GET /ws` (WebSocket) | Everything else. The first message must be `connectDevice`; anything else first closes the socket |
| `GET /admin/ws` (WebSocket) | Desktop editor. Requires the persistent admin token (see ARCHITECTURE.md, Pairing and authentication); admin-scoped commands and queries are accepted only here |

### 3.2 Envelope

Every WebSocket message is one JSON object with a `kind` and a `type` discriminator:

```json
{ "kind": "command", "type": "executeAction", "requestId": "c0f1…", "payload": { … } }
```

The agent writes `kind` first and `type` second; readers accept the fields in any order. System.Text.Json cannot
nest two discriminators, so `MessageConverter` handles `kind` and each kind's base record handles `type` with
`[JsonPolymorphic]`.

| `kind` | Direction | Fields |
|---|---|---|
| `command` | client → agent | `requestId`, `type`, `payload` |
| `query` | client → agent | `requestId`, `type`, `payload` |
| `ack` | agent → client | `requestId`, optional `payload` (only `registerApp`: `{ appId }`; omitted otherwise) |
| `error` | agent → client | `requestId` (`null` when the request could not be read), `code`, `message` |
| `queryResult` | agent → client | `requestId`, `type`, `payload` |
| `event` | agent → client | `type`, `payload` (no `requestId`) |

`protocolVersion` starts at `1` and is sent in `connectDevice`; a mismatch returns `protocolMismatch` and closes.

### 3.3 Messages

**Commands** (payload)

| `type` | Payload |
|---|---|
| `connectDevice` | `{ token, protocolVersion }` |
| `executeAction` | `{ profileId, pageIndex, buttonId }` |
| `saveProfile` | `{ profile: ProfileDto }` |
| `deleteProfile` | `{ profileId }` |
| `startPairing` / `cancelPairing` | `{}` |
| `renameDevice` | `{ deviceId, name }` |
| `revokeDevice` | `{ deviceId }` |
| `updateSettings` | `{ settings: SettingsUpdateDto }` |
| `registerApp` | `{ displayName, path }` |
| `unregisterApp` | `{ appId }` |
| `saveIcon` | `{ iconId, data }` (base64 PNG, Phase 7) |
| `deleteIcon` | `{ iconId }` (Phase 7) |

HTTP `POST /api/pairing/redeem`: `PairDeviceRequest { code, deviceName }` → `200 PairDeviceResponse { deviceId, deviceToken }`
or `400 ErrorDto`. `pairDevice` never travels over the WebSocket, so its result is not an `ack`.

**Queries → results**

| Query `type` | Payload | Result `type` | Result payload |
|---|---|---|---|
| `getProfiles` | `{}` | `profiles` | `{ profiles: ProfileSummaryDto[] }` |
| `getProfile` | `{ profileId }` | `profile` | `{ profile: ProfileDto }` |
| `getDevices` | `{}` | `devices` | `{ devices: DeviceDto[] }` |
| `getPairingSession` | `{}` | `pairingSession` | `{ session: PairingSessionDto \| null }` |
| `getSettings` | `{}` | `settings` | `{ settings: SettingsDto }` |
| `getAgentStatus` | `{}` | `agentStatus` | `{ agentVersion, protocolVersion, obsConnected }` |
| `getInstalledGames` | `{}` | `installedGames` | `{ games: InstalledGameDto[] }` (Phase 7) |
| `getIcons` | `{}` | `icons` | `{ icons: string[] }` (Phase 7) |

**Events**

| `type` | Payload |
|---|---|
| `profileUpdated` | `{ profile: ProfileDto }` |
| `profileDeleted` | `{ profileId }` |
| `devicesChanged` | `{ devices: DeviceDto[] }` (admin connections only) |
| `deviceRevoked` | `{ deviceId }` — sent to the revoked device, then its socket closes |
| `pairingSessionChanged` | `{ session: PairingSessionDto \| null }` (admin connections only) |
| `settingsUpdated` | `{ settings: SettingsDto }` (admin connections only) |
| `obsSceneChanged` | `{ sceneName }` (Phase 7) |

**DTOs**

| DTO | Fields |
|---|---|
| `ProfileSummaryDto` | `id`, `name`, `color` |
| `ProfileDto` | `id`, `name`, `color`, `grid: { columns, rows }`, `pages: PageDto[]` |
| `PageDto` | `index`, `buttons: ButtonDto[]` |
| `ButtonDto` | `id`, `row`, `column`, `label`, `color`, `iconId`, `action: ActionDto` |
| `ActionDto` | Discriminated by `type`: `hotkey { modifiers[], key }`, `mediaKey { key }`, `launchApp { appId }`, `openUrl { url }`, `obsScene { sceneName }`, `launchGame { store, gameId }`, `changePage { target: next \| previous \| specific, pageIndex }` (`pageIndex` only for `specific`, otherwise `null`), `switchProfile { profileId }` |
| `DeviceDto` | `id`, `name`, `pairedAt`, `lastSeenAt`, `connected`, `revoked` |
| `PairingSessionDto` | `code`, `qrUrl`, `fallbackUrl`, `expiresAt` |
| `SettingsDto` | `startWithWindows`, `defaultGrid`, `obs: { host, port, hasPassword } \| null`, `apps: { appId, displayName, path }[]` |
| `SettingsUpdateDto` | `startWithWindows`, `defaultGrid`, `obs: { host, port, password } \| null` — apps are managed by `registerApp`/`unregisterApp`; `password: null` keeps the stored one, `""` clears it |
| `ErrorDto` | `code`, `message` |

Ids are GUID strings. Dates are ISO 8601 UTC strings with milliseconds (`2026-10-22T08:30:00.125Z`). Enum values
are camelCase strings (`pageUp`, `volumeUp`); integers are rejected. The OBS password is write-only: `updateSettings`
may set it (`SettingsUpdateDto`), results only expose `hasPassword`. Reads are strict: missing constructor fields,
`null` in non-nullable fields, unknown `kind`/`type` values and unknown action types fail with a `JsonException`.

**Error codes:** `unauthorized`, `forbidden` (wrong scope), `protocolMismatch`, `validationFailed`, `notFound`,
`pairingCodeInvalid`, `pairingCodeExpired`, `deviceRevoked`, `actionFailed`, `internal`.

Domain codes map onto them in `Infrastructure/ProtocolMapping/ErrorMapper`: every invariant failure
(`DuplicateProfileName`, `LastProfileCannotBeDeleted`, `Position*`, `GridTooSmall`) is `validationFailed`;
`ProfileNotFound`/`DeviceNotFound`/`ButtonNotFound`/`IconNotFound` (Phase 7) are `notFound`; `ActionNotExecutable` and `ActionFailed` are
`actionFailed`; anything unexpected is `internal` with a generic message.

### 3.4 Tasks

- [x] All records above in `LeverDeck.Protocol` with a source-generated `JsonSerializerContext`.
- [x] Polymorphic (de)serialization for the envelope (`kind` + `type`) and `ActionDto` via `type` discriminators.
- [x] Mappers DTO ⇄ domain in `Infrastructure`, with tests (invalid DTOs become `validationFailed`, never exceptions).
- [x] Confirm TypeGen (or pick an alternative) and generate `web/src/app/core/protocol/*.ts`, including the
      discriminated unions. TypeGen 7.0.0 was rejected (no literal `type` fields or unions, `Date` for ISO strings,
      numeric enums); `agent/tools/LeverDeck.ProtocolGen` generates the files from the serializer contract. It runs
      on demand (command in CLAUDE.md), and a test fails the build pipeline when the committed files are stale.
- [x] Contract tests: JSON round-trip for every message and a snapshot of the generated TypeScript.

**Done when:** every message in 3.3 round-trips; changing a record without regenerating fails `dotnet test`; the
generated folder is never edited.

## Phase 4 — Windows adapters

- [x] `SendInputActionExecutor` via CsWin32: hotkeys and media keys. `LaunchApp`, `OpenUrl`, `ObsScene` and
      `LaunchGame` fail with `actionNotExecutable` until Phase 7. Architecture test: every action in `ActionCatalog.All`
      is classified by `ActionExecutorCoverage` (the deferred half of 2.4).
- [x] JSON stores under `%APPDATA%\LeverDeck\` (`profiles.json`, `devices.json`, `settings.json`) with a seeded default
      profile on first run. Atomic writes, one lock per file, `schemaVersion`; a corrupt file fails loudly and is never
      overwritten. Persistent DPAPI-protected admin token in `admin-token`; the OBS password is DPAPI-protected too.
- [x] mDNS: register `leverdeck.local` with `DnsServiceRegister` (CsWin32) behind the `ServiceAdvertiser` port, as a
      hosted service that re-advertises on address changes (off with `LeverDeck__Mdns__Enabled=false`).
- [x] Integration tests for the stores (temp folder). DI wiring of the 18 use cases in `LeverDeck.App`, plus a
      Development-only, loopback-only `POST /debug/execute` compiled into Debug builds only.
- [x] Manual check: input reaching the PC through `/debug/execute` — the "Vol -" media key lowered the volume twice
      (HTTP 200 `{"ok":true}`, confirmed by the owner, 2026-10-22).
- [ ] Manual check (owner): `ping leverdeck-<pc>.local` (the name the agent logs) from another device.

**Done when:** a debug endpoint can trigger a hotkey on the PC, and `ping leverdeck.local` resolves from another device.

## Phase 5 — PWA skeleton (Angular)

- [x] `ng new web` (standalone, strict), `ng add @angular/pwa`, ESLint with `eslint-plugin-boundaries`
      (features never import each other; shared code via `core/`). Angular CLI 22, zoneless, Vitest; the boundaries
      config also keeps presentational components away from facades and adapters.
- [x] `core/connection`: one WebSocket service with reconnect/backoff and a `connected | reconnecting | offline` signal.
      Port + WebSocket adapter + facade; backoff 0.5 s doubling to a 30 s cap with jitter; `offline` while the device
      has no network. Same-origin `ws:`/`wss:` URL on `/ws` (the endpoint itself arrives in Phase 6).
- [x] Theme tokens from [VISUAL-REFERENCE.md](VISUAL-REFERENCE.md) as CSS custom properties (dark first, light via
      `prefers-color-scheme`), and an app shell showing "LeverDeck" and the connection status.
- [x] Build output embedded in the agent with `ManifestEmbeddedFileProvider`, with an SPA fallback to `index.html`
      (never under `/api`, `/ws`, `/admin/ws`, `/debug`, `/health`), `no-cache` for `index.html`, `ngsw*` and the
      manifest, one-year `immutable` for hashed bundles. Without a web build the agent embeds a fallback page.
- [ ] Manual (owner): open `http://leverdeck-<pc>.local:8765` on the phone and see the shell load from the exe.

**Done when:** opening `http://leverdeck.local:8765` on the phone loads the PWA from the exe.

## Phase 6 — First vertical slice: pair and press

- [x] PC side: pairing page with QR (`/pair?code=…` + IP fallback) and expiring one-time code, as in
      [mocks/desktop.html](mocks/desktop.html).
- [x] Phone side: `features/pairing` (scan or type code), as in [mocks/login.html](mocks/login.html); store the token.
- [x] Phone side: `features/deck` renders the active profile grid; tapping a key sends `executeAction`.
- [x] Agent authenticates every WebSocket with the token hash; unknown or revoked tokens are rejected.
      `POST /api/pairing/redeem`, `/ws` (device, `connectDevice` first) and `/admin/ws` (admin token) route every
      message to its use case; the connection registry feeds `connected`, events fan out by scope and a revocation
      closes the device's sockets (endpoints, close codes and routing in ARCHITECTURE.md, Transport endpoints).
- [x] `leverdeck.local` resolves: `DnsServiceRegister` published the service but Windows answered no A query for
      its host, so the agent runs its own mDNS responder for the A record (ARCHITECTURE.md, Connectivity). Manual
      check (owner): `Resolve-DnsName leverdeck.local` on the PC and `http://leverdeck.local:8765` on the phone.
- [ ] Manual test over LAN and over USB (`adb reverse tcp:8765 tcp:8765`).

**Done when:** a freshly paired phone presses a key and the PC receives the hotkey. **This is the MVP.**

## Phase 7 — Make it usable

- [x] Desktop editor: profiles sidebar, grid editor, selected-key panel, paired devices with revoke. The PC edits,
      devices only read (see `odd/tasks/phase-7-make-it-usable.md`).
- [x] More actions: open URL, launch registered apps, launch Steam/Epic games (installed games listed by
      `getInstalledGames`). Executed by `AgentActionExecutor` through the Windows shell.
- [ ] OBS scene (confirm OBS client AOT compatibility). Postponed: not used by the owner.
- [x] Multiple pages per profile and the grid size (3×3, 5×3, 8×4; new profiles take the 5×3 default setting) in
      the editor, plus dragging keys between positions and pages.
- [x] Tray icon, start with Windows, firewall rule on the private profile (tray not yet checked on screen).
- [x] Optional HTTPS over LAN with `mkcert` for installable PWA (`LeverDeck:Https:Enabled`; not yet tested with mkcert).

## Phase 8 — Release

- [ ] Publish `leverdeck.exe` from CI as a GitHub Release. The workflow is ready (`.github/workflows/release.yml`:
      a `v*` tag builds, tests, publishes and attaches the exe); done once the first tag is released.
- [x] The exe and the tray carry the app icon (`LeverDeck.App/leverdeck.ico`, from the PWA icon).
- [ ] README: install, firewall, pairing, USB mode, known limits (anti-cheat, elevated apps — see HANDOFF.md).
- [ ] Fill the **Commands** section of `CLAUDE.md`.

## Phase 8b — Desktop app (installer and own window)

LeverDeck installs and runs like Spotify or Discord: a setup program installs it per user, the Start menu entry opens
**its own window** with the existing editor (no browser, no URL to copy, no console), and closing the window keeps the
agent in the tray serving the phones. The UI stays the Angular editor; only where it is shown changes (decided
2026-10-23, option A over rewriting the editor in a native UI toolkit).

- [x] Spike: a WebView2 window inside the Native AOT exe versus WinForms + the official package published
      single-file. **Decided: Native AOT.** Source-generated COM (`[GeneratedComInterface]` / `[GeneratedComClass]`,
      IIDs and vtable order from the package's `WebView2.h`) with `WebView2LoaderStatic.lib` linked in through
      `DirectPInvoke`: one 1.9 MB exe, no trim/AOT warnings, ~0.4 s to the first page, ~21 MB working set; the
      single-file WinForms route works too but ships 117 MB and uses ~49 MB. Notes for the move into `LeverDeck.App`:
      the tray thread must become STA and host the window too (one UI thread; Kestrel unaffected); the environment
      callback can arrive re-entrantly; call `controller.Close()` before exit; set per-monitor v2 DPI before any
      window and resize on `WM_SIZE`/`WM_DPICHANGED`; always pass the WebView2 user data folder
      (`<DataDirectory>/webview2`), since the default next to the exe is replaced on each Velopack update; a failing
      `CreateCoreWebView2EnvironmentWithOptions` HRESULT is where a missing runtime is reported.
- [ ] GUI subsystem: `OutputType` `WinExe` in Release (no console window), `Exe` in Debug so `dotnet run` keeps its
      logs; Release logs go to a file under `<DataDirectory>/logs`.
- [ ] Logs never grow without bound: every start of the agent (Windows sign-in, or quitting from the tray and opening
      it again) deletes every existing log and begins a new one; no previous log is kept (owner's decision). Closing
      only the editor window is not a start. A size cap (for example 10 MB) applies within a session, since a tray
      session can run for weeks.
- [ ] Editor window: opens `http://localhost:8765/admin` with the admin token (never shown to the user), remembers its
      size and position, closing it hides it and keeps the tray; the tray menu gains "Open LeverDeck".
- [ ] Single instance: a second launch (Start menu, shortcut) asks the running instance to show its window and exits.
- [ ] Start at login launches in the background (tray only); a manual launch opens the window.
- [ ] Installer and updates with Velopack: per-user install under `%LocalAppData%`, Start menu shortcut, clean
      uninstall, updates from GitHub Releases; the release workflow publishes `LeverDeckSetup.exe` next to the exe.
- [ ] WebView2 runtime: present on Windows 10/11 through Windows Update; detect a missing runtime and say how to get it.
- [ ] Manual check (owner): install, open from the Start menu, close to tray, reopen, update to a newer tag, uninstall.

Progress (2026-10-23): every item above is built on branch `claude/amazing-curie-nxci18` and none is ticked, since
each needs a Windows run. Verified in a Linux container: both configurations build with no warnings, Release is
`WinExe`, the Native AOT compile of the whole app (Velopack included) shows no trim or AOT warnings and links the static
WebView2 loader, the log, placement and launch rules pass their tests, and `vpk pack` produces the installer files.
Updates from GitHub Releases need the releases to be public (the repository is private today). Evidence and the open
checks: `odd/tasks/desktop-app.md`.

## Phase 8c — Sound clips (built, not checked on Windows)

Play short sounds on the PC from a key. The owner uploads a clip from the desktop editor, names it, assigns it to a key
with an icon, and presses the key on the phone; the PC plays the clip through its speakers. The phone only sends the
press (`executeAction`), as for every other key. Phones never upload, rename or delete a clip. Recording on the phone's
microphone is a later phase, not part of this one.

Decided (owner, 2026-10-23): all configuration happens on the PC; **pressing again restarts the clip from the beginning**,
like the soundboard panel in Discord (no toggle). The design artifact "LeverDeck Audio" was the reference for the
screens.

Deliberate deviations from the design artifact:

- **WAV only.** The design lists MP3, OGG and FLAC. The Windows sound API that plays here (winmm `PlaySound`) reads only
  uncompressed PCM WAV and does not decode the rest, so the editor refuses them with the way to convert them. Supporting
  them needs a decoder (Media Foundation or similar), which is an AOT and dependency question for the owner to decide.
- **One clip at a time.** `PlaySound` plays one clip; a press on another clip cuts the current one. Several clips at once
  need another API (WASAPI or XAudio2), which is more native code.
- **No "Add to key" button, no waveform, no drop zone.** A clip is assigned in the key's action editor (the Sound picker),
  which is where the editor already picks apps and games. The waveform in the design is decorative and was left out.
- **The phone's Audio screen is a read-only list** (name, length, size). It has no play buttons and no bottom navigation:
  preview needs the admin token, which a phone does not hold, and the header link to `/sounds` replaces the tab bar.

Design notes as built:

- **Closed action.** `PlaySound` (`abstract record Action` subtype) carries a `SoundId` (`sound-` and 32 hex digits), never a
  path. The architecture test and the executor coverage list it.
- **Upload over HTTP.** `PUT /api/sounds/<id>.wav?name=…` with the admin token in `X-LeverDeck-Admin`, because a 50 MB clip
  does not fit the 1 MiB admin socket. Checked as a PCM WAV in the domain (`WaveFile`): format, mono or stereo, 8–32 bits,
  a data chunk, 50 MB at most. The library, rename and delete use the socket; every connection hears `soundsChanged`.
- **Storage.** `sounds.json` (names, lengths, sizes; schema-versioned like the other files) and `sounds/<id>.wav`, written
  atomically. A clip a key still plays cannot be deleted, as an icon.
- **Playback.** `PlaySound` through winmm, asynchronous (`SND_ASYNC`): the second press restarts the clip. Windows needs no
  administrator rights.

### 8c.1 Tasks

- [x] Domain: `SoundId`, `SoundName`, `WaveFile`, `SoundClip`, `PlaySound`, with tests (test-first).
- [x] Application: `GetSounds`, `UploadSound`, `RenameSound`, `DeleteSound` behind `ISoundStore` and `ISoundPlayer`, with fakes
      and tests; `ScopeTests` covers which scope may run each one (uploads and changes need admin; the list needs any paired caller).
- [x] Infrastructure: `FileSoundStore` (index and files), `WinmmSoundPlayer` (CsWin32 `PlaySound`), `SoundEndpoint` (upload and
      admin preview), mappers, persistence records, executor routing.
- [x] Protocol and transport: `playSound` action; `renameSound`, `deleteSound`, `getSounds` and their results; `soundsChanged`;
      TypeScript regenerated.
- [x] Web, editor: Audio section (choose WAV, name, upload, preview, rename, delete), Sound picker in the key's action editor.
- [x] Web, phone: read-only Audio list at `/sounds`, linked from the header.
- [ ] Owner, on Windows: upload a WAV from the PC, assign it to a key, press from the phone, hear it on the PC, press it
      again while it plays (it restarts), rename it, delete it while a key uses it (refused), delete it when unused.
- [ ] Owner, on Windows: a 50 MB WAV uploads, and a larger file or an MP3 is refused with its message.

## Phase 9 — macOS agent (planned, not started)

Run the same agent on a Mac, with the Mac's own app catalog in the Apps view. The PWA and the protocol stay shared;
everything OS-specific gets a macOS adapter behind the existing ports. Do not start until the owner asks
(CLAUDE.md: Windows is the only platform in scope).

### 9.1 What is Windows-only today

| Area | Windows today | macOS replacement |
|---|---|---|
| Project targets | `LeverDeck.Infrastructure` and `LeverDeck.App` are `net10.0-windows`; CsWin32 (`NativeMethods.txt`) lives in Infrastructure | Split into a shared Infrastructure plus `Infrastructure.Windows` / `Infrastructure.MacOS` (or multi-target), the composition root picks one with `OperatingSystem.IsMacOS()` |
| Hotkeys and media keys (`IActionExecutor`: `SendInputActionExecutor`, `KeyCodes`) | `SendInput` with virtual-key codes | `CGEventCreateKeyboardEvent` + `CGEventPost` (CoreGraphics), mac virtual key codes, media keys as `NX_SYSDEFINED` events; needs the **Accessibility** permission |
| Launch apps and URLs (`ShellLauncher`, `LaunchUris`) | Shell execute, `shell:AppsFolder\<AUMID>` for Store apps | `open -b <bundle id>` / `NSWorkspace` for apps, `open <url>` for URLs |
| Store app catalog (`IStoreAppCatalog`: `StartMenuStoreAppCatalog`) | Start menu query for AppUserModelIds | Scan `/Applications`, `/System/Applications`, `~/Applications` for `.app` bundles (name and `CFBundleIdentifier` from `Info.plist`); Mac App Store apps are the ones with `Contents/_MASReceipt` (or `mdfind "kMDItemAppStoreHasReceipt == 1"`) |
| Registered app target (`RegisteredApp`, Domain) | Absolute path, or `shell:AppsFolder\<AUMID>` | A third form for bundles, e.g. `bundle:<CFBundleIdentifier>`, validated like the AUMID (closed pattern, never a command) |
| Games (`IGameLibrary`: Steam, Epic) | `libraryfolders.vdf`, `%ProgramData%` Epic manifests | Steam keeps `libraryfolders.vdf` under `~/Library/Application Support/Steam/steamapps` (the VDF reader is reusable); Epic's manifest folder on macOS still to be verified |
| Start with the OS (`IStartupRegistration`: Run key) | `HKCU\...\Run` | A LaunchAgent plist in `~/Library/LaunchAgents` (or `SMAppService` on macOS 13+, which needs an `.app` bundle) |
| Admin token protection (`SecretProtector`) | DPAPI | Keychain (`SecItemAdd` / `SecItemCopyMatching`, Security.framework) |
| mDNS (`DnsServiceAdvertiser` + own responder on UDP 5353) | `DnsServiceRegister` plus our own A-record responder | The system `mDNSResponder` owns 5353: register through `dns_sd` (`DNSServiceRegister` for the service, `DNSServiceRegisterRecord` for the `leverdeck-<pc>.local` A record) and drop the own responder there |
| Tray (`TrayIcon`, `NativeTray`) | Win32 notification icon | Menu bar item (`NSStatusItem`) through the Objective-C runtime (`objc_msgSend`, AOT-safe `LibraryImport`); `LSUIElement` so there is no Dock icon |
| Firewall (`FirewallRuleService`, `PrivateFirewallRule`) | `netsh` rule on the private profile | Nothing to add: the macOS application firewall asks once on first listen, and remembers a signed app |
| Data directory | `%APPDATA%\LeverDeck` via `Environment.GetFolderPath` | `~/Library/Application Support/LeverDeck`; check which `SpecialFolder` maps there on .NET 10 before relying on it |
| Domain and protocol naming | `StartWithWindows`, Store tab copy ("Microsoft Store", ".exe", "Copy as path") | `StartAtLogin` (protocol change + regenerated TypeScript); the agent reports its platform so the web shows "Mac App Store" / "Applications" and macOS path hints |
| Hotkey model | Ctrl / Alt / Shift / Win | Decide how keys map: Win ↔ Cmd, Alt ↔ Option, or per-platform modifiers on the key |

### 9.2 Packaging and distribution

- Native AOT does not cross-compile to macOS: build on a macOS runner (`macos-latest`, Apple Silicon) for `osx-arm64`
  (and `osx-x64`, joined with `lipo` into a universal binary, if Intel Macs matter).
- Wrap the binary in `LeverDeck.app` (`Info.plist` with `CFBundleIdentifier`, `LSUIElement`, an `.icns` made from the
  PWA icon) and ship it as a `.dmg` or `.zip` from the same GitHub Release workflow.
- **Not through the Mac App Store:** App Store apps must run in the App Sandbox, which does not allow posting keyboard
  events to other apps (Accessibility) or launching arbitrary apps; the agent's core job would not work there. Mac App
  Store apps are still supported as *targets* (9.1, Store app catalog).
- **Signing:** Gatekeeper blocks unsigned downloads. Options: Developer ID signing + notarization (Apple Developer
  Program, paid yearly; best experience, the Accessibility grant survives updates), or free ad-hoc signing
  (`codesign -s -`; the owner opens it with right-click › Open or clears the quarantine attribute, and macOS asks for
  Accessibility again after each new build because the grant is tied to the signature).
- **First-run setup on the Mac:** grant Accessibility (System Settings › Privacy & Security › Accessibility) so hotkeys
  work; allow incoming connections when the firewall asks; start at login from the menu bar item.

### 9.3 Tasks (when it starts)

- [ ] Split Infrastructure by OS without changing the ports; Windows tests stay green.
- [ ] `bundle:` target in `RegisteredApp` (test-first), catalog of `.app` bundles with Mac App Store flag.
- [ ] macOS adapters: keyboard and media keys, launcher, Keychain secret, LaunchAgent, `dns_sd` advertiser, menu bar.
- [ ] Platform in the protocol (agent → web) and platform-aware copy in the Apps dialog and settings.
- [ ] macOS job in CI and in the release workflow (`.app` bundle, signing per the decision below).
- [ ] Manual check on a Mac: pair a phone, press a hotkey, launch a Mac App Store app.

## Open decisions to settle along the way

| Decision | Settle by |
|---|---|
| ~~TypeGen vs alternative for TS generation~~ — decided: own generator in `agent/tools/LeverDeck.ProtocolGen` (see 3.4) | Phase 3 |
| ~~How the desktop editor proves it is the PC~~ — decided: a persistent admin token (ARCHITECTURE.md, Pairing and authentication) | Phase 3 |
| Native AOT vs single-file publish | Phase 1 spike |
| Default grid size and max profiles shown | Phase 7 |
| Light theme beyond system preference | Phase 7 |
| Anti-cheat and elevated-target handling | After MVP (HANDOFF.md future ideas) |
| macOS signing: Developer ID + notarization (paid) vs free ad-hoc signing (conflicts with HANDOFF "Cost: zero") | Phase 9 |
| macOS hotkey modifiers: map Win ↔ Cmd, or per-platform modifiers on each key | Phase 9 |
