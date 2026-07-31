# ONI Agent Plugin

A mod for *Oxygen Not Included* that turns a running colony into an
observation + action interface for AI agents: it exposes live colony state
over a local HTTP API (duplicants, buildings, resources, critical events,
environment) and accepts a batch of control commands back (dig, build,
pause/unpause), applied safely on Unity's main thread. The mod itself does
no reasoning — any agent or framework that can make HTTP requests can poll
state and issue commands, with no dependency on any particular backend.

It happens to have first-class support for pushing snapshots to and
receiving commands from **Ledgyx** (see [Optional: Ledgyx
integration](#optional-ledgyx-integration) below) — that's what it was
originally built to demo — but that integration is one client of the same
plain HTTP API documented here, not a requirement. Point your own agent
loop at the endpoints in [HTTP API](#http-api) and skip the Ledgyx-specific
pieces entirely if you don't use it.

## Status

Both stages are live:

- **Stage 1 (observation):** four snapshot tiers (duplicants, colony,
  critical events, environmental) collected on independent cadences, served
  locally over HTTP. Optionally also pushed to Ledgyx.
- **Stage 2 (control):** a command queue accepts `dig_rect`, `build`, and
  `set_paused` batches via a direct local `POST /api/command` — and, if
  Ledgyx integration is configured, also via Ledgyx's SSE channel
  (`oni_command` event type) — and applies them on Unity's main thread.

## Using it with your own agent

The mod's entire surface is the local HTTP API in [HTTP API](#http-api)
below — nothing about it assumes Ledgyx, or any particular agent framework.
A typical loop:

1. **Observe** — `GET` one or more of the `/api/snapshot/*` endpoints to
   read current colony/duplicant/environment state.
2. **Decide** — your agent's own reasoning, outside this mod entirely.
3. **Act** — `POST /api/command` with a batch of `dig_rect`/`build`/
   `set_paused` items.
4. **Confirm** — `GET /api/command/result?batch_id=...` to see what
   actually happened (each item succeeds or fails independently, with a
   message).

That loop works standalone, with zero Ledgyx configuration. The optional
Ledgyx push clients (`LedgyxPushClient`, `CriticalEventPushClient`,
`EnvironmentalPushClient`) and the SSE client (`LedgyxSseClient`) all check
their own settings (API key / endpoint / SSE token) before doing anything,
and cleanly no-op with a one-time log warning if those are left blank in
`settings.json` — you don't need to remove or disable any code to run this
against your own agent instead, just leave the Ledgyx-specific fields
empty.

## Architecture

```
OniAgentMod.cs          entry point — UserMod2.OnLoad, Harmony bootstrap,
                         wires up every ticker/client below

ApiServer.cs             HttpListener on localhost:9813 — no external HTTP
                          deps. Serves cached snapshots, accepts commands.

Snapshot/
  SnapshotTicker.cs       MonoBehaviour, ticks each tier on its own cadence
                          (LateUpdate) — never blocks the sim
  *Collector.cs           one collector per tier, reads live game objects
  SnapshotCache.cs         last-collected snapshot per tier, read by ApiServer
                           and the push clients; never mutated after publish

Commands/
  CommandQueue.cs          thread-safe queue — HTTP listener thread enqueues,
                           never touches game state directly
  CommandTicker.cs          MonoBehaviour, drains the queue on the main thread
  CommandExecutor.cs        applies one CommandItem to live game state
  CommandResultCache.cs     recent batch results, polled via
                           GET /api/command/result

Networking/              optional — Ledgyx integration only, see
                         "Optional: Ledgyx integration" below. Every client
                         here no-ops (with a one-time log warning) if its
                         settings are left unconfigured.
  LedgyxPushClient.cs        POSTs the operational-tier snapshot to Ledgyx
                              on a cron-style timer
  CriticalEventPushClient.cs  event-driven — pushes each critical event the
                               moment it's detected, no cadence of its own
  EnvironmentalPushClient.cs  POSTs the environmental tier on its own timer
  LedgyxSseClient.cs          subscribes to Ledgyx's SSE stream; delivers
                              agent-run results locally and forwards
                              "oni_command" events into CommandQueue

Settings/
  AgentSettings.cs           all tunables (endpoints, API key, cadences)
  SettingsManager.cs          loads settings.json next to the DLL, writes
                              defaults on first run, clamps cadences to
                              sane minimums

lib/                        vendored reference DLLs (Assembly-CSharp,
                             0Harmony, UnityEngine.*, Newtonsoft.Json) —
                             not committed, see lib/README.md
```

Key constraints baked into the design:

- Snapshot collection and command execution both run on Unity's main thread
  via dedicated `MonoBehaviour` tickers (`SnapshotTicker`, `CommandTicker`),
  ticking every N seconds rather than every frame.
- The HTTP listener runs on its own background thread; anything it receives
  that touches game state goes through `CommandQueue` first — `Grid`,
  `Assets`, and `BuildingDef.Build` are not thread-safe.
- Every snapshot payload carries a `SchemaVersion`, since this schema is
  meant to double as a stable observation space for whatever agent reasons
  over it — Ledgyx's or your own.
- Cached snapshot/response objects are never mutated in place after being
  published to `SnapshotCache` — a consumer reading mid-collection can't see
  a half-written object.

## Building

Requires the .NET SDK (cross-platform — the built DLL does not need to be
rebuilt per OS) and the vendored game assemblies in `lib/` (see
`lib/README.md` for exactly which DLLs and where to copy them from).

```bash
dotnet build
```

Reference assemblies aren't committed (`lib/*.dll` is gitignored) — copy them
once per machine from the Mac install:

```
OxygenNotIncluded.app/Contents/Resources/Data/Managed/
```

## Installing

Copy the built `OniAgent.dll` plus `mod_info.yaml` into ONI's mods folder on
the machine running the game:

```
~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/
```

(verify the exact path via the in-game Mod Manager). `settings.json` is
written next to the DLL on first load with defaults — the per-tier cadence
fields are the only ones you need for standalone use against your own
agent (see [Using it with your own agent](#using-it-with-your-own-agent)
above). `LedgyxEndpoint`/`ApiKey`/`SseEndpoint`/`SseToken`/
`CriticalEventsEndpoint`/`EnvironmentalEndpoint` only matter if you're
using the optional Ledgyx integration — leave them blank otherwise.

## HTTP API

The mod listens on `http://localhost:9813/` — only reachable on the machine
actually running the game.

### Snapshots (GET)

| Endpoint | Returns |
|---|---|
| `/api/snapshot/duplicants` | per-duplicant state |
| `/api/snapshot/colony` | colony/building/resource state |
| `/api/snapshot/critical` | recent critical events (oxygen, etc.) |
| `/api/snapshot/environmental` | tile temp/mass/element, aggregated into sectors |
| `/api/agent-run/latest` | most recent agent-run result delivered over SSE |
| `/api/settings` | non-secret view of loaded settings |

Each returns the last value collected by `SnapshotTicker`, not a fresh
read — check the response's `SchemaVersion`/`Cycle` fields for freshness.

### Commands

`POST /api/command` — enqueue a batch, returns immediately with a
`batch_id`; `CommandTicker` executes it on the next main-thread tick.

```json
{
  "commands": [
    { "type": "dig_rect", "x1": -15, "x2": 15, "y1": -5, "y2": -1 },
    { "type": "build", "building": "Ladder", "x": 0, "y": -1 },
    { "type": "set_paused", "paused": true }
  ]
}
```

All coordinates are cell offsets relative to the Duplicant Printing Pod
(Telepad) — not absolute world cells.

Supported command types:

- **`dig_rect`** (`x1`, `x2`, `y1`, `y2`) — queues a normal Dig chore over
  the inclusive rectangle; duplicants dig it over time like any other dig
  order. Skips cells adjacent to liquid (coarse 4-neighbor check) to avoid
  breaching a reservoir.
- **`build`** (`building`, `x`, `y`) — places a building already complete,
  skipping the ghost/haul-materials/construct chore chain. Refuses to build
  if the footprint overlaps an existing building.
- **`set_paused`** (`paused`) — pauses/unpauses the game via
  `SpeedControlScreen`. Idempotent: repeating the same value is a no-op
  rather than drifting the game's internal (reference-counted) pause state.

`GET /api/command/result?batch_id=...` — poll a specific batch's outcome.
`GET /api/command/results` — 10 most recent batches, newest first.

### Manual testing

`test-command.sh` sends a first-floor test batch (dig a room, build a ladder
shaft, place a wash basin and outhouse) to a running instance:

```bash
ONI_AGENT_HOST=http://localhost:9813 ./test-command.sh
```

Run it on the machine where the game and mod are actually running — the API
only listens on localhost.

## Optional: Ledgyx integration

If you set `LedgyxEndpoint`/`ApiKey` (and, for the critical/environmental
tiers, `CriticalEventsEndpoint`/`EnvironmentalEndpoint`) in `settings.json`,
`LedgyxPushClient`/`CriticalEventPushClient`/`EnvironmentalPushClient` push
the corresponding snapshot tiers to Ledgyx on their own cadences
(`PushCadenceSeconds`, event-driven, `EnvironmentalPushCadenceSeconds`
respectively). Ledgyx's ingestion channel fires one AI_AGENT run per row
pushed, so this is the path that lets a Ledgyx-hosted agent actually reason
over colony state.

If you additionally set `SseEndpoint`/`SseToken`, `LedgyxSseClient` opens a
persistent SSE connection back to Ledgyx and does two things with it:
delivers agent-run results locally (`GET /api/agent-run/latest`), and
forwards any `oni_command` SSE event straight into `CommandQueue` — the
same entry point, validation, and execution path as a direct
`POST /api/command`. In other words, a Ledgyx agent controls the colony by
calling Ledgyx's own generic `send_sse_notification` tool with
`event_type="oni_command"`, not a bespoke per-project tool.

None of this is required for the observe/decide/act loop described in
[Using it with your own agent](#using-it-with-your-own-agent) — it's purely
how *this* project currently wires a Ledgyx-hosted agent to the same API.

## Known limitations

- `build` is instant-complete; it bypasses the normal
  ghost/haul/construct chore, so duplicants never actually build anything
  themselves for agent-issued builds.
- Command coordinates are Telepad-relative, but nothing yet lets an agent
  query fine-grained (per-cell) environment data before choosing where to
  dig or build — the environmental snapshot tier is sector-aggregated
  (32 cells/side by default) and too coarse to catch single-cell hazards.
  Dig-time safety checks (liquid adjacency, footprint overlap) exist as a
  server-side safety net, but there's no equivalent query the agent can use
  proactively yet.
- No idempotency/dedup for retried `POST /api/command` calls or repeated SSE
  deliveries of the same event.
- Mod Options screen integration isn't implemented — `settings.json` is a
  plain file next to the DLL, edited by hand.
