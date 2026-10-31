# OBS integration: backend

Locator: `odd/tasks/obs-integration.md` · Engram mirror: `odd/obs-integration/tasks` · Branch: `feat/obs-backend`

## Objective

A button can start or stop the stream and the recording, switch the OBS scene, mute an input, and show or hide a
source in a scene. The agent talks to OBS WebSocket v5 on its own, and the agent reports whether OBS is connected and
which scenes exist, so the editor can offer a scene picker later.

## Problem / why

Settings already store the OBS connection (host, port, password protected with DPAPI) and `ObsScene` exists, but the
executor answers "OBS actions arrive later", no client talks to OBS, and no action starts or stops the stream.

## Decisions

- **One record per verb with an enum**, like `MediaKey`: `ObsStream(Start|Stop|Toggle)`, `ObsRecord(...)`,
  `ObsInputMute(InputName, Mute|Unmute|Toggle)`, `ObsSourceVisibility(SceneName, SourceName, Show|Hide|Toggle)`.
  `ObsScene` stays as it is.
- **OBS client written by hand** over `System.Net.WebSockets` with `System.Text.Json` (`Utf8JsonWriter` and
  `JsonDocument`, no reflection), so it stays Native AOT safe. No third-party OBS library.
- The client is an Application port (`IObsClient`); the WebSocket adapter lives in `Infrastructure/Obs`. The executor
  routes the OBS actions to it, and `GetAgentStatus` reads its connection state.
- A background worker keeps the connection alive: it retries every few seconds while settings have an OBS connection.
  Commands fail fast with `actionFailed` when OBS is not connected; they do not wait for a reconnect.
- Events: `obsSceneChanged` (already in the protocol) and a new `obsStateChanged` `{ streaming, recording }`, both fed
  by OBS event subscriptions (Scenes and Outputs).
- Query `getObsScenes` (device scope) returns `{ scenes, currentScene }` for the future scene picker.
- Protocol version stays at `1`: nothing is released yet. Revisit before the first release.
- Out of scope for this task: replay buffer, virtual camera, pause recording, scene collections, and every frontend
  change (the web app gets its own design task for the scene picker).

## Scope

- `agent/src/LeverDeck.Domain/Actions`: four records, three enums, `ObsScene` sharing the name rule, `ActionCatalog`.
- `agent/src/LeverDeck.Application`: `Obs/Ports/IObsClient`, `Obs/GetObsScenes`, `Status` gets `ObsConnected`, two
  application events.
- `agent/src/LeverDeck.Protocol`: four action DTOs, three enum DTOs, `getObsScenes` query and result, `obsStateChanged`.
  Regenerate `web/src/app/core/protocol` with `tools/LeverDeck.ProtocolGen` (the snapshot test needs it; no hand edits).
- `agent/src/LeverDeck.Infrastructure`: `Obs/` (protocol, client, worker), `Input/AgentActionExecutor` routing,
  `Persistence` records and mapper, `ProtocolMapping` for actions, status and events, `DependencyInjection`.
- `agent/src/LeverDeck.App/CompositionRoot.cs`: the query use case.
- Tests in each layer, plus a local fake OBS server for the client.
- `docs/ARCHITECTURE.md` and `docs/ROADMAP.md` (close the OBS item at line 342).

## Acceptance criteria

- Each new action validates its parameters in the domain and round-trips through the protocol and persistence.
- With a fake OBS server: the client authenticates (SHA-256 challenge), sends each request with its `requestId`, maps
  failed responses to `actionFailed`, reports `ObsConnected`, and publishes scene and state events.
- Executing an OBS action while OBS is not connected returns `actionFailed` with a clear message; no exception escapes.
- `dotnet test LeverDeck.slnx` passes, and the TypeScript snapshot matches the regenerated protocol.

## Checks

From `agent/`: `dotnet build LeverDeck.slnx`, `dotnet test LeverDeck.slnx`. From `agent/`:
`dotnet run --project tools/LeverDeck.ProtocolGen -- ../web/src/app/core/protocol`, then the tests again.

Not checked here: a real OBS instance. The OBS request names and fields come from the obs-websocket v5 protocol
document (fetched while planning); a manual test against OBS is still needed.

## Delivery

Strategy: `ask-on-risk`. Forecast about 900 authored changed lines, split into work-unit commits and PR slices of about
400 lines: (1) domain, application and tests; (2) protocol and generated types; (3) infrastructure, executor, wiring.

## Tasks

- [ ] T1 Domain: `ObsStream`, `ObsRecord`, `ObsInputMute`, `ObsSourceVisibility` and their enums; catalog; tests first.
- [ ] T2 Application: `IObsClient`, `GetObsScenes`, `ObsConnected` in status, `ObsSceneChanged` and `ObsStateChanged`
      application events; tests with a fake client.
- [ ] T3 Protocol: action DTOs and enum DTOs, `getObsScenes`, `obsStateChanged`, wire samples; regenerate TypeScript.
- [ ] T4 Infrastructure mapping: `ActionMapper`, persistence records, `EventFanOut`, `StatusMapper`; tests.
- [ ] T5 OBS client: `ObsProtocol` (pure framing and auth), `ObsWebSocketClient`, `ObsConnectionWorker`; tests with a
      fake server.
- [ ] T6 Executor and wiring: route the OBS actions, `ActionExecutorCoverage`, DI registration, query use case; tests.
- [ ] T7 Docs: ARCHITECTURE and ROADMAP.
- [ ] T8 Checks: build, tests, regenerated protocol; work-unit commits on `feat/obs-backend`.
