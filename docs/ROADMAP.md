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
      TypeScript conventions are added with the Angular app in Phase 5.
- [x] Add a GitHub Actions workflow on `windows-latest` that builds, tests and AOT-publishes `agent/`.
      The `web/` job is added in Phase 5.

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

**Queries** (no side effects)

| Query | Scope | Returns |
|---|---|---|
| `GetProfiles` | device | Profile summaries: id, name, color |
| `GetProfile` | device | One full profile |
| `GetDevices` | admin | Devices with name, paired/last-seen dates, connected and revoked state |
| `GetPairingSession` | admin | Active code, QR URL (with IP fallback) and expiry, or none |
| `GetSettings` | admin | `AgentSettings` |
| `GetAgentStatus` | device | Agent version, protocol version, OBS connection state |

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
`ProfileNotFound`/`DeviceNotFound`/`ButtonNotFound` are `notFound`; `ActionNotExecutable` and `ActionFailed` are
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
- [ ] Manual check (owner): a hotkey reaching the PC through `/debug/execute`, and `ping leverdeck.local` from another
      device. They cannot run unattended; the debug command is in CLAUDE.md.

**Done when:** a debug endpoint can trigger a hotkey on the PC, and `ping leverdeck.local` resolves from another device.

## Phase 5 — PWA skeleton (Angular)

- [ ] `ng new web` (standalone, strict), `ng add @angular/pwa`, ESLint with `eslint-plugin-boundaries`
      (features never import each other; shared code via `core/`).
- [ ] `core/connection`: one WebSocket service with reconnect/backoff and a `connected | reconnecting | offline` signal.
- [ ] Theme tokens from [VISUAL-REFERENCE.md](VISUAL-REFERENCE.md) as CSS custom properties.
- [ ] Build output embedded in the agent with `ManifestEmbeddedFileProvider`.

**Done when:** opening `http://leverdeck.local:8765` on the phone loads the PWA from the exe.

## Phase 6 — First vertical slice: pair and press

- [ ] PC side: pairing page with QR (`/pair?code=…` + IP fallback) and expiring one-time code, as in
      [mocks/desktop.html](mocks/desktop.html).
- [ ] Phone side: `features/pairing` (scan or type code), as in [mocks/login.html](mocks/login.html); store the token.
- [ ] Phone side: `features/deck` renders the active profile grid; tapping a key sends `executeAction`.
- [ ] Agent authenticates every WebSocket with the token hash; unknown or revoked tokens are rejected.
- [ ] Manual test over LAN and over USB (`adb reverse tcp:8765 tcp:8765`).

**Done when:** a freshly paired phone presses a key and the PC receives the hotkey. **This is the MVP.**

## Phase 7 — Make it usable

- [ ] Desktop editor: profiles sidebar, grid editor, selected-key panel, paired devices with revoke.
- [ ] More actions: launch app, OBS scene (confirm OBS client AOT compatibility), then Steam/Epic games.
- [ ] Multiple pages per profile; grid size setting (decide the default — still open in PRODUCT.md).
- [ ] Tray icon, start with Windows, firewall rule on the private profile.
- [ ] Optional HTTPS over LAN with `mkcert` for installable PWA.

## Phase 8 — Release

- [ ] Publish `leverdeck.exe` from CI as a GitHub Release.
- [ ] README: install, firewall, pairing, USB mode, known limits (anti-cheat, elevated apps — see HANDOFF.md).
- [ ] Fill the **Commands** section of `CLAUDE.md`.

## Open decisions to settle along the way

| Decision | Settle by |
|---|---|
| ~~TypeGen vs alternative for TS generation~~ — decided: own generator in `agent/tools/LeverDeck.ProtocolGen` (see 3.4) | Phase 3 |
| ~~How the desktop editor proves it is the PC~~ — decided: a persistent admin token (ARCHITECTURE.md, Pairing and authentication) | Phase 3 |
| Native AOT vs single-file publish | Phase 1 spike |
| Default grid size and max profiles shown | Phase 7 |
| Light theme beyond system preference | Phase 7 |
| Anti-cheat and elevated-target handling | After MVP (HANDOFF.md future ideas) |
