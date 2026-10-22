# LeverDeck Roadmap

How to go from docs-only to a working app. Each phase ends in something runnable and committed; do not start a phase
until the previous one's **Done when** holds. Read [ARCHITECTURE.md](ARCHITECTURE.md) first; this plan only orders the
work.

Guiding rule: build **one thin vertical slice end to end** (phone tap → hotkey on the PC) before widening anything.

## Phase 0 — Tooling and repo hygiene

- [x] `.gitattributes` with LF line endings.
- [ ] Install the .NET 10 SDK, Node LTS, Angular CLI, and the "Desktop development with C++" workload (needed for
      Native AOT on Windows).
- [ ] Add `.editorconfig` (C# and TypeScript conventions) and a root `.gitignore` (`bin/`, `obj/`, `node_modules/`,
      `dist/`, `.atl/*.cache.json`).
- [ ] Add a GitHub Actions workflow that builds and tests `agent/` and `web/` on `windows-latest`.

**Done when:** a clean clone builds in CI with empty projects.

## Phase 1 — Agent skeleton (.NET)

- [ ] Create `agent/LeverDeck.sln` with `src/LeverDeck.Domain`, `Application`, `Protocol`, `Infrastructure`, `App`.
- [ ] Wire the dependency rule as project references only (`App → Infrastructure → Application → Domain`,
      `Infrastructure → Protocol → Domain`). Domain and Application reference no ASP.NET or OS packages.
- [ ] Create `tests/LeverDeck.Domain.Tests`, `tests/LeverDeck.Application.Tests` and
      `tests/LeverDeck.ArchitectureTests` (xUnit).
- [ ] Architecture test: assert the project reference rule (for example with NetArchTest or plain reflection).
- [ ] `App`: minimal API on port `8765` with `GET /health` returning `ok`.
- [ ] **AOT spike:** `dotnet publish -c Release -r win-x64 -p:PublishAot=true` produces one `leverdeck.exe` that serves
      `/health`. Record the binary size and any trim/AOT warnings. If a blocker appears, switch to self-contained
      single-file and note why in HANDOFF.md.

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

- [ ] Domain types and invariants above, each with unit tests written first.
- [ ] Architecture test: every `Action` subtype is handled by the executor routing and present in the whitelist.
- [ ] Ports as interfaces in Application, plus in-memory fakes in the test projects.
- [ ] Every command and query as one class with one `Execute` method, RED → GREEN against the fakes.
- [ ] Tests for the security rules: revoked device rejected, expired/reused code rejected, `ExecuteAction` ignores any
      action not stored in the profile, admin use cases unreachable from device scope.

**Done when:** every use case in 2.3 passes against fakes; no adapter code yet.

## Phase 3 — Wire contract and TypeScript generation

`LeverDeck.Protocol` holds DTOs only (C# records, camelCase JSON, System.Text.Json source generation). It maps to and
from the domain in `Infrastructure`; domain types never go on the wire.

### 3.1 Transport

| Channel | Used for |
|---|---|
| `POST /api/pairing/redeem` (HTTP, no auth) | `PairDevice` — the only call made before the device has a token |
| `GET /ws` (WebSocket) | Everything else. The first message must be `connectDevice`; anything else first closes the socket |
| `GET /admin/ws` (WebSocket) | Desktop editor. Admin-scoped commands and queries are accepted only here (see open decisions) |

### 3.2 Envelope

Every WebSocket message is one JSON object with a `kind` and a `type` discriminator:

```json
{ "kind": "command", "requestId": "c0f1…", "type": "executeAction", "payload": { … } }
```

| `kind` | Direction | Fields |
|---|---|---|
| `command` | client → agent | `requestId`, `type`, `payload` |
| `query` | client → agent | `requestId`, `type`, `payload` |
| `ack` | agent → client | `requestId`, optional `payload` (only for `pairDevice`) |
| `error` | agent → client | `requestId`, `code`, `message` |
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
| `updateSettings` | `{ settings: SettingsDto }` |
| `registerApp` | `{ displayName, path }` |
| `unregisterApp` | `{ appId }` |

HTTP `POST /api/pairing/redeem`: request `{ code, deviceName }` → `200 { deviceId, deviceToken }` or `400 ErrorDto`.

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
| `ActionDto` | Discriminated by `type`: `hotkey { modifiers[], key }`, `mediaKey { key }`, `launchApp { appId }`, `openUrl { url }`, `obsScene { sceneName }`, `launchGame { store, gameId }`, `changePage { target }`, `switchProfile { profileId }` |
| `DeviceDto` | `id`, `name`, `pairedAt`, `lastSeenAt`, `connected`, `revoked` |
| `PairingSessionDto` | `code`, `qrUrl`, `fallbackUrl`, `expiresAt` |
| `SettingsDto` | `startWithWindows`, `defaultGrid`, `obs: { host, port, hasPassword } \| null`, `apps: { appId, displayName, path }[]` |
| `ErrorDto` | `code`, `message` |

Dates are ISO 8601 UTC strings. The OBS password is write-only: `updateSettings` may set it, results only expose
`hasPassword`.

**Error codes:** `unauthorized`, `forbidden` (wrong scope), `protocolMismatch`, `validationFailed`, `notFound`,
`pairingCodeInvalid`, `pairingCodeExpired`, `deviceRevoked`, `actionFailed`, `internal`.

### 3.4 Tasks

- [ ] All records above in `LeverDeck.Protocol` with a source-generated `JsonSerializerContext`.
- [ ] Polymorphic (de)serialization for the envelope and `ActionDto` via `type` discriminators.
- [ ] Mappers DTO ⇄ domain in `Infrastructure`, with tests (invalid DTOs become `validationFailed`, never exceptions).
- [ ] Confirm TypeGen (or pick an alternative) and generate `web/src/app/core/protocol/*.ts` on build, including the
      discriminated unions.
- [ ] Contract tests: JSON round-trip for every message and a snapshot of the generated TypeScript.

**Done when:** every message in 3.3 round-trips; changing a record and building updates the generated TypeScript; the
generated folder is never edited.

## Phase 4 — Windows adapters

- [ ] `SendInputActionExecutor` via CsWin32: hotkeys and media keys.
- [ ] JSON stores under `%APPDATA%\LeverDeck\` (`profiles.json`, `devices.json`, `settings.json`) with a seeded default
      profile on first run.
- [ ] mDNS: register `leverdeck.local` with `DnsServiceRegister` (P/Invoke) behind the `ServiceAdvertiser` port.
- [ ] Integration tests for the stores (temp folder); manual check for input and mDNS.

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
| TypeGen vs alternative for TS generation | Phase 3 |
| How the desktop editor proves it is the PC: with `adb reverse`, a phone also connects from loopback, so "localhost = admin" is not safe. Candidate: an admin token created at startup and opened from the tray | Phase 3 |
| Native AOT vs single-file publish | Phase 1 spike |
| Default grid size and max profiles shown | Phase 7 |
| Light theme beyond system preference | Phase 7 |
| Anti-cheat and elevated-target handling | After MVP (HANDOFF.md future ideas) |
