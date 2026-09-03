# Headless-Test Harness v0.2 Implementation Design

## Status and scope

This document records the implemented D0-D6 design for the 0.111 adapter. It is
the repository-local operational companion to the full Chinese architecture
note `22-杀戮尖塔2全命令行无头测试方案调研`.

The implementation proves three real-runtime milestones:

1. PoC 0: launch and authenticate a broker-owned 0.111 game instance in H0
   (`headless` display, `Dummy` audio).
2. PoC 1: create a ShinGetter run, enter a deterministic encounter, play an
   exact typed no-choice card action, and compare canonical pre/post state.
3. PoC 1b: keep a parent card request in flight across a real card selection,
   reject stale/busy continuations, accept the exact server-issued selection,
   and settle the parent action.
4. Runtime release gate: disconnect and reconnect during that in-flight choice,
   prove duplicate/conflict/terminal-replay behavior in the production C#
   companion, classify shutdown diagnostics, seal evidence, and require exact
   game exit code 0 with no broker leak.

The branch does not implement D7-D11: save/load/replay, multiplayer automation,
issue #191 RED/GREEN, animation/resource stepping, H1 pixel output, release
packaging, virtual time, or the 0.109 adapter.

## Repository decision and isolation

The harness lives at the independent top-level `headless-test-harness/` on a
feature branch of the ShinGetterMod repository. This supersedes the earlier
proposal for a separate Git repository, while preserving every logical and
release boundary:

- no production source project references the companion;
- no production manifest, DLL, PCK, ZIP, or four-file deployment contains it;
- reverse-engineered game trees are read-only inputs;
- runtime sessions, user data, logs, saves, screenshots, staging, and evidence
  live outside all repositories and deployment trees;
- the Python harness, C# companion, schemas, and golden fixtures are versioned
  atomically in this one top-level directory.

`IMPORT_BASELINE.json` freezes the 14-file CLI-Anything 0.1.0 import and its
canonical aggregate SHA-256 before v0.2 changes.

## Component layout

```text
headless-test-harness/
  bridge/Sts2HeadlessTestBridge/
    package/                 TEST-ONLY mod manifest
    profiles/                0.111 build profile
    src/Bootstrap/           opt-in and root-node lifecycle
    src/Contract/            framing, canonical JSON, HMAC, error contract
    src/Dispatch/            main-thread requests, actions, choices
    src/Security/            environment and writable-root validation
    src/State/               snapshots, handles, ShinGetter extension
    src/Transport/           duplex named-pipe server
    tests/                   ContractVerifier and ComponentHost
  python/agent-harness/
    cli_anything/.../core/   session, broker, process, protocol, evidence, diff
    cli_anything/.../commands/
    tests/
  schemas/                   protocol/state/scenario/evidence v1
  fixtures/golden/           cross-language deterministic examples
  scripts/                   baseline import and production reverse scan
```

The live data flow is:

```text
CLI invocation
  -> authenticated local control pipe
  -> one long-lived broker per control session
  -> one authenticated duplex pipe per game instance
  -> TEST-ONLY companion
  -> bounded main-thread dispatcher
  -> real 0.111 game API and ActionQueue
```

## Trust and authentication model

The companion starts only when `STS2_TEST_ENABLE=1` and all required instance,
session, pipe, token, and output-root inputs pass validation. These variables
are injected only into a broker-owned disposable game process.

The broker holds the random token in memory. It is not placed on the command
line, persisted in `session.json`, echoed through CLI output, or included in
evidence. Client and server prove knowledge of the token with distinct HMAC
transcripts covering session, instance, process epoch, connection, negotiated
protocol, resume sequence, and both nonces. The client acknowledges the signed
hello before command traffic is accepted.

JSONL limits apply before deserialization: UTF-8 framing, no BOM/NUL, bounded
line length, object depth, string length, and array size. Every accepted
envelope conforms to `sts2-test/v1`. Unknown commands, invalid enum values,
major-version mismatch, replayed nonces, identity mismatch, and tampered proofs
fail deterministically without exposing secret material.

## Control session and process ownership

A control session is a directory whose direct parent is passed as
`--runtime-root`. It contains a locked `session.json`, broker identity,
append-only request and broker-event journals, and one directory per instance.

The broker owns each started process by the full identity tuple:

```text
PID + process start time UTC + executable path + executable SHA-256
```

Status and stop operations revalidate that tuple. A mismatch returns
`E_PROCESS_IDENTITY_MISMATCH`; no operation falls back to killing by name. On
Windows, instances are assigned to a Job Object for broker-exit cleanup. A new
broker cannot adopt an old game process or recreate the old in-memory token.

Each instance has independent `APPDATA`, `LOCALAPPDATA`, logs, snapshots, and
bridge events. Writable-root guards reject repository, reverse-engineered game,
Steam, Workshop, production mod, symlink, and reparse-point boundaries.
Existing sessions revalidate all persisted protected roots whenever they are
opened. Their named category/path mapping is persisted and canonical-hashed; the
same mapping is required from the CLI, and the broker receives that expected
digest at bootstrap. The game child is constructed from a minimal
operating-system environment allowlist plus explicit TEST and isolated-user-data
variables; it does not inherit the broker's ambient environment wholesale.

Every live CLI invocation must explicitly provide
`--protect-production-source`, `--protect-steam-game`,
`--protect-workshop`, and `--protect-production-mod`. The selected 111 project
and harness repository are protected implicitly. Missing categories, duplicate
or redundant identities, nonexistent/relative paths, and policy changes on open
fail closed with `E_ISOLATION_BREACH`.
An optional fifth category, `--protect-production-deployment`, covers a distinct
normal deployment or user-data tree. When present it participates in the same
canonical policy document, persisted identity, overlap validation, and broker
bootstrap digest.
On Windows the guard checks the unresolved lexical ancestor chain before
`Path.resolve()`, checks the resolved chain, and checks again after directory
creation, so a junction cannot disappear from inspection by being resolved to
its target.

## Transport, reconnect, and idempotency

The broker maintains the long-lived companion connection so separate CLI
invocations can participate in one in-flight request. It multiplexes requests
and events by request ID and sequence number.

The same request ID plus the same canonical request digest attaches to the
original in-flight execution. Completed request IDs and their digests remain as
tombstones for the entire companion process epoch, while only the 256 most
recent terminal payloads are retained in an LRU. Inside that payload window an
exact retry replays the first terminal. Outside it, an exact retry returns
`E_IDEMPOTENCY_WINDOW_EXPIRED` and never executes again. The digest covers the
complete request, including `command`, `args`, `wait_for`, and `timeout_ms`,
while excluding volatile transport metadata such as sequence, clock,
connection, and broker epoch. The same ID plus different content always returns
`E_IDEMPOTENCY_CONFLICT`, including after terminal-payload eviction; neither
failure replaces the original tombstone.

The broker-owned Python client mirrors the same request-ID/canonical-digest
ledger for its entire authenticated process epoch. A different payload is
rejected before a second pipe write, closing the race where a late terminal from
the original in-flight request could arrive after a new attempt's sequence
floor. Allowed exact retries still record a sequence floor and accept only an
`accepted` or terminal envelope newer than it, so terminals already buffered
before that retry cannot satisfy it.

Each authenticated connection has its own bounded live critical outbound queue
and writer pump. Replay is a separate rolling store, so normal retention
eviction does not invalidate a case. Reconnect is allowed only to the same live
broker and process epoch. A cursor older than the current replay floor returns
`E_RESUME_WINDOW_EXPIRED`; otherwise retained events are replayed in sequence.
Malformed or oversized frames terminate only the offending connection, while
the server remains available for a fresh authenticated connection. On Windows,
the Python reader pump probes the Named Pipe before reading so a blocking read
on a duplex handle cannot starve writes.

Monotonic deadlines continue across choice and reconnect; they never restart
from a fresh timeout budget.

Unexpected process exit synthesizes `E_PROCESS_EXIT` for all in-flight
requests. Broker exit is `E_BROKER_EXIT`. Unsafe cancellation is rejected as
`E_CANCEL_UNSAFE`.

Graceful `runtime.shutdown` is advisory to exact process ownership. A failed or
synthetic-failed shutdown terminal is raised as `ProtocolFailure`, captured in
the per-instance `shutdown_error`, and the manager still waits, terminates, then
kills the exact owned process if required. `session.close` returns every
per-instance stop result so cleanup failures remain inspectable.

## Main-thread execution and concurrency

Pipe I/O and authentication never touch Godot objects. Valid requests enter a
bounded dispatcher and execute in `BridgeRootNode._Process()` on the Godot main
thread.

Commands declare one of three concurrency classes:

- `snapshot-safe-query`: may run while a gameplay parent is waiting;
- `gameplay-mutation`: acquires the single mutation lane until terminal;
- `choice-continuation`: may pass only when it matches the parent that owns the
  lane.

Critical event publication uses non-blocking `TryWrite`. Only failure to publish
to the active connection's bounded live queue latches the case invalid, freezes
mutation, and emits the terminal `E_OBSERVER_OVERFLOW` through a separate
unbounded out-of-band lane. Rolling replay eviction is not overflow. The writer
pump stops request intake when it exits, and serialized pipe writes are guarded
by one semaphore. Telemetry overflow is counted separately and cannot hide
critical loss.

## Implemented companion commands

| Command | Class | Completion | v0.2 behavior |
|---|---|---|---|
| `runtime.ping` | query | immediate | Frame, wall clock, dispatcher depth, main-thread ID |
| `runtime.capabilities` | query | immediate | Tri-state runtime capabilities |
| `runtime.commands` | query | immediate | Registered command descriptors |
| `runtime.shutdown` | control | immediate | Flush and request companion shutdown |
| `state.dump` | query | immediate | Persist canonical state and return its ID/hash/path |
| `run.new` | mutation | location predicate + settled | New unsaved single-player standard run |
| `run.status` | query | immediate | Run epoch, seed, save flag, location |
| `console.exec` | mutation | location predicate + settled | Exact allowlist: `fight <encounter-id>` |
| `combat.status` | query | immediate | Combat epoch, location, queue mirror |
| `combat.add_card` | mutation | awaitable command + settled | Add exactly one model to `Hand` and issue its handle |
| `combat.play_card` | mutation | exact typed action + settled | Single-player `PlayCardAction` using server handles |
| `choice.list` | query | immediate | Current choice descriptor and candidate handles |
| `choice.select` | continuation | immediate | Resume the matching `CardSelectCmd` selector |

`run.new` accepts character ID, seed, and ascension and always uses
`shouldSave=false`. `combat.play_card` accepts a combat-card handle and an
optional creature handle. It validates owner, phase, pile, target, and
`CanPlay` before enqueueing.

## Exact action completion

`ActionObserver` subscribes to the real run's `ActionQueueSet` and
`ActionExecutor`. Before enqueue, `combat.play_card` registers the exact newly
created `PlayCardAction` object reference. After `RequestEnqueue`, the reference
must already be observed with a real action ID or the request fails with
`E_ACTION_CORRELATION_FAILED`. There is no frame-window or nearest-action
fallback.

Completion levels are intentionally distinct:

- `enqueued`: exact action observed in the queue;
- `action_finished`: its own `CompletionTask` completed and action state is
  `Finished`;
- `queue_settled`: every relevant queue is empty, the executor is idle, no
  choice is pending, the operation task completed without exception, the
  command-specific predicate holds, and the post-snapshot succeeded.

The `fight` predicate requires actual `PlayerTurnPhase.Play`; a completed
DevConsole task plus an empty queue is not sufficient while combat initialization
still reports `PlayerTurnPhase.None`.

## Stable state and handles

`SnapshotBuilder` produces canonical `sts2-state/v1` JSON with:

- authoritative combat projection and game checksum;
- partial deterministic run projection;
- local semantic players, piles, cards, enemies, powers, actions, choices, and
  locations;
- a reflection-only `shin-getter-test/v1` extension for form, vigor, evolution,
  chain reaction, saved properties, and presentation availability;
- process/run/room/combat epochs, provenance, completeness, and SHA-256 hashes.

Canonicalization sorts object keys and explicitly set-like arrays while
preserving semantic order for piles and action sequences. Secret, credential,
account, and machine-specific values are redacted. Snapshotting records the RNG
fingerprint before and after capture and fails if observation consumes gameplay
RNG.

Handles name their expiry scope rather than a global snapshot revision.
Combat-card and creature handles expire on combat change; player handles expire
on run/process change; room handles expire on room change; choice handles and
candidates use a per-owner choice generation. Unrelated snapshot revisions do
not invalidate still-live objects.

## Choice continuation

`ChoiceBroker` implements the game's `ICardSelector` and is installed with
`CardSelectCmd.UseSelector(..., localOnly: true)`. v0.2 therefore exposes this
path only for single-player local choice.

The required external sequence is:

```text
runtime dispatch combat.play_card
  -> event choice_required(parent request ID, owner, generation, handles)
runtime exec choice.select(matching parent and server-issued handles)
  -> selector_accepted=true
runtime wait-terminal(parent request ID)
  -> completed / queue_settled
```

Parent, owner, generation, choice handle, candidate handles, cardinality, and
terminal state are checked. A stale or mismatched continuation returns
`E_STALE_HANDLE`. While the parent waits, unrelated mutation returns
`E_MUTATION_BUSY`. Parent failure or timeout invalidates the pending choice so a
late continuation cannot revive a terminal request.

## Evidence and release isolation

Evidence finalization validates the manifest schema, relative path confinement,
file hashes, redaction, and required provenance before atomic publication.
Verification recomputes every covered hash; one changed byte returns
`E_EVIDENCE_TAMPERED`. Explicit snapshots are included, while the isolated
user-data tree and account-bearing save data are excluded.

Starting with D2, production inputs and artifacts are reverse-scanned for bridge
assembly names, TEST environment variables, protocol IDs, overflow/idempotency
errors, and component-host markers. The gate must cover the production
source/build-input tree and representative DLL/PCK/ZIP outputs, and must return
`hits=[]`.

## Build and verification

From the repository worktree:

```powershell
dotnet build headless-test-harness\bridge\Sts2HeadlessTestBridge\profiles\Sts2TestBridge.111.csproj -c Release

dotnet run --project headless-test-harness\bridge\Sts2HeadlessTestBridge\tests\ContractVerifier\ContractVerifier.csproj -c Release -- headless-test-harness

dotnet build headless-test-harness\bridge\Sts2HeadlessTestBridge\tests\ComponentHost\ComponentHost.csproj -c Release
```

Install the Python package editable, then run the complete suite from a cwd
outside both repository and package source:

```powershell
$env:CLI_ANYTHING_FORCE_INSTALLED = '1'
python -m pytest <absolute-path-to-agent-harness-tests> -v -s -rs --tb=no
```

The real-runtime release gate is separate and opt-in:

```powershell
$env:CLI_ANYTHING_FORCE_INSTALLED = '1'
$env:STS2_HEADLESS_RUNTIME_RELEASE_GATE = '1'
$env:STS2_HEADLESS_RUNTIME_PROFILE = '<absolute-external-profile.json>'
python -m pytest <absolute-path-to-agent-harness-tests>\test_runtime_release.py -v -s --tb=no
```

The 2026-09-03 third-review remediation suite collected 267 nodes and produced
265 passed plus two explicit skips in 50.50 seconds: the privilege-dependent
Windows symlink fixture and the opt-in real-runtime test. Release mode without
a profile failed as required. The configured real gate then passed in 58.16
seconds, both C# builds completed with zero
warnings/errors, and the cross-language contract verifier passed. A separate
forced-installed subprocess check printed the resolved console script path.
The code and regression checkpoint is
`5526ff6d33b0675315c94aee57c273cc1c500266`; final runtime evidence was
captured only after that commit existed.

## Real-runtime proof

The final release gate ran `v0.111.0@41cef1ea` in `headless / Dummy` using an
external disposable staging tree. It rebuilt and staged companion SHA-256
`337fc7cab7152112735fd35b9eefe978b6b7a6cbd618cab0ce78e6120ad9c7a8`.

PoC 1 proved exact no-choice completion: energy `3 -> 2`, block `0 -> 5`, empty
queue, idle executor, no pending choice, and unchanged RNG fingerprint.

PoC 1b played real `ARMAMENTS`, observed five real candidates, disconnected at
the choice boundary, and reconnected to the same process epoch with a new
connection ID and replay status `ok`. An exact in-flight duplicate did not
execute again; changed content returned `E_IDEMPOTENCY_CONFLICT` without
poisoning the parent. The run then rejected one stale generation and one
unrelated mutation, accepted the matching selection, upgraded the selected
ShinGetter Defend from `0 -> 1`, and settled the parent. The event sequence was
enqueued `24`, choice required `26`, action finished `39`, and terminal `40`;
an exact post-terminal duplicate returned the same result with `replayed=true`
at sequence `41`. Pre/post snapshots are `solo-11.json` and `solo-12.json`.

The finalized session is
`E:\Work\StS2 Mods\_headless-runtime\release-gates\runtime-release-20260903-fe1d7c46`.
Its verified evidence manifest contains 22 artifacts with aggregate SHA-256
`fcd46b7d191848d3cb8b5b6073b31b11df01d9abd9b94ff3c6f2af8231345644`.

The final production reverse scan also covers
`E_IDEMPOTENCY_WINDOW_EXPIRED` as a forbidden TEST-ONLY signature. Its report is
`C:\Users\win\AppData\Local\cli-anything\slaythespare2-111-beta\gates\20260903-third-review-remediation-production-scan.json`;
all seven signatures returned `hits=[]` across the production input tree, DLL,
PCK, and release ZIP.

The instance stopped gracefully with exact exit code 0, known Godot shutdown
diagnostics were classified, the broker accepted `session close`, and no game,
broker, pytest, or ComponentHost process remained.
No reverse-engineered source, Steam installation, normal Godot deployment, or
production package was modified.

## Operational limitations

- H0 reports pixel output as `unknown` and virtual clock as `unavailable`.
- The run-state projection is intentionally partial; combat checksum remains a
  separate authoritative oracle.
- Card selection is single-player `LocalSelector` only.
- DevConsole execution is an exact one-command allowlist, not a general console
  tunnel.
- The feature branch is reviewable but is not authorization to merge, tag,
  release, deploy, or copy the companion into production.
