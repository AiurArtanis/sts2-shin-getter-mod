# Slay the Spire 2 111-beta Harness SOP

## Purpose

This harness gives agents a deterministic command-line surface over the real
111-beta source tree and real TEST-ONLY game runtime. It does not reimplement
game behavior in Python.

## Non-negotiable boundaries

- Treat all reverse-engineered game trees as read-only inputs.
- Put runtime sessions, user data, logs, saves, screenshots, staging packages,
  and evidence outside every repository, Steam, Workshop, and production mod
  directory.
- For every live command, repeat `--protected-root` for all source, Steam,
  Workshop, production-mod, and production-deployment roots. Values must be
  existing absolute directories; omission is `E_ISOLATION_BREACH`.
- Never add the companion to `ShinGetterMod.csproj` or a production manifest,
  PCK, ZIP, or four-file deployment.
- Start and stop live processes through the broker. Process identity is PID +
  process start time + executable path/hash; never kill by process name.
- Do not expose the companion token in argv, environment reports, logs, JSON
  output, or evidence. The broker keeps it in memory.
- v0.2 live mutation is single-player only. Multiplayer, save/load/replay,
  animation/pixel gates, virtual time, H1, and the 0.109 adapter are out of
  scope.

## Real backends

- Source inspection uses the files under the selected project root and `rg`.
- Semantic inspection calls the repository's existing `codegraph` index.
- Builds call `dotnet` against `sts2.csproj`.
- Import and launch commands call a Godot 4.5 Mono executable.
- DevConsole commands are indexed from their C# definitions. They are not
  executed outside the live game because their runtime state is unavailable.
- Live mutations are executed by the companion on the Godot main thread and
  call real `RunManager`, DevConsole, `ActionQueueSynchronizer`,
  `PlayCardAction`, and `CardSelectCmd` APIs.
- Snapshots combine the game's `NetFullCombatState` checksum with a canonical
  semantic projection and a reflection-only ShinGetter extension.

## Command surface

- `source status|files|search|args`
- `graph status|sync|query|explore`
- `build status|restore|run`
- `game doctor|import|launch|smoke`
- `console list|search|show`
- `session status|configure|undo|redo|close`
- `process start|list|status|stop`
- `runtime handshake|connect|exec|dispatch|wait-event|wait-terminal|request-status`
- `evidence finalize|verify`

Every command accepts the root-level `--json` option. `--dry-run` is available
only for `graph sync`, `build restore`, `build run`, `game import`, `game smoke`,
`game launch`, `session configure`, `session undo`, and `session redo`; those
commands report the proposed backend command or state change. Live
broker/companion mutations do not expose `--dry-run`.

## Session addressing

For live commands, set all four roots explicitly:

```powershell
$common = @(
  '--project-root', '<read-only-111-source>',
  '--state-dir', '<external-cli-state>',
  '--runtime-root', '<external-session-parent>',
  '--control-session', 'case-001',
  '--protected-root', '<read-only-111-source>',
  '--protected-root', '<read-only-production-source>',
  '--protected-root', '<steam-game-root>',
  '--protected-root', '<workshop-root>',
  '--protected-root', '<production-mod-root>',
  '--json'
)
```

`--runtime-root` is the session directory's direct parent. A session ID is a
validated identifier, not a path. The protection list is intentionally
explicit and repeatable; the harness cannot infer every local deployment tree.

## State and safety

Legacy CLI state defaults to
`%LOCALAPPDATA%/cli-anything/slaythespare2-111-beta/state`, outside the checkout.
Configuration writes are protected by an inter-process lock, saved atomically,
and recorded in a bounded undo/redo history. Source files are read-only from
the harness.

`graph sync`, `build`, and Godot import/launch can update backend-owned caches or
build outputs. They never edit decompiled C# source. `game launch --detach` is
the only command that intentionally leaves a process running.

The live broker uses an external `session.json`, locked JSONL journals, and one
instance directory per process. On Windows, broker-owned game processes are
assigned to a Job Object. A broker replacement must not adopt an old process or
recover its in-memory token. Opening an existing session revalidates every
persisted protected root before any write. A game child receives only a small
system environment allowlist plus explicit test and isolated-user-data values;
the broker's ambient environment is not inherited wholesale.

Runtime-path validation checks unresolved ancestors, resolved ancestors, and
the post-create path. Symlinks and Windows junctions are rejected even when
resolution would otherwise erase the reparse node from the candidate path.

## Live lifecycle

1. Build the 0.111 companion in Release and stage it only in a disposable game
   copy outside the repositories and normal deployment locations.
2. Start the process with `process start`; pass the executable and Godot/game
   arguments after the command's options. The broker injects only its allowlisted
   TEST variables and isolated `APPDATA`/`LOCALAPPDATA`.
3. Confirm `process status`, `runtime handshake`, adapter/game fingerprints,
   `display_driver=headless`, `audio_driver=Dummy`, and the reported user-data
   path before creating a run.
4. Use `runtime exec` for queries and non-branching mutations. Request
   `wait_for=queue_settled` for gameplay completion.
5. For a selection-producing card, use `runtime dispatch`, wait for the exact
   `choice_required` event, submit `choice.select` with its parent request ID,
   owner, generation, choice handle, and candidate handles, then wait for the
   parent terminal result.
6. Finalize and verify evidence if the case is publishable.
7. Stop each exact instance with `process stop`; then call `session close`.

A graceful-shutdown RPC failure does not cancel exact cleanup. The stop result
records a structured `shutdown_error`, then continues through wait,
terminate, and kill fallbacks against the verified process identity.

Reconnect uses a rolling critical-event replay window. A cursor older than its
reported floor must fail with `E_RESUME_WINDOW_EXPIRED`; do not silently restart
observation. The live outbound critical queue is a distinct bounded resource:
only a blocked active consumer that exhausts it latches
`E_OBSERVER_OVERFLOW`, freezes mutation, and invalidates the case.

## Completion and handle rules

- `enqueued` only proves the exact typed action entered the queue.
- `action_finished` proves that exact action's `CompletionTask` completed.
- `queue_settled` additionally requires empty queue sets, idle executor, no
  pending choice, a satisfied command-specific ready predicate, and successful
  post-snapshot capture.
- Stable handles survive unrelated state revisions but expire at their declared
  process/run/room/combat or per-owner choice generation.
- A gameplay parent retains the mutation lane while waiting for its choice.
  Only the matching `choice.select` continuation and snapshot-safe queries may
  pass; unrelated mutation fails with `E_MUTATION_BUSY`.
- Reusing a request ID is valid only with the complete same canonical request,
  including `wait_for` and `timeout_ms`. Exact in-flight duplicates do not
  execute again. The 256 newest terminal payloads can be replayed; their request
  ID/digest tombstones remain for the whole process epoch. An exact retry whose
  payload was evicted returns `E_IDEMPOTENCY_WINDOW_EXPIRED`, while different
  content returns `E_IDEMPOTENCY_CONFLICT`; neither path executes or overwrites
  the original tombstone.

## Evidence

Evidence finalization validates every path, hash, required schema field, and
redaction rule before atomically publishing the manifest. `evidence verify`
must fail with `E_EVIDENCE_TAMPERED` after any covered byte changes. Do not edit
an evidence directory after finalization. Explicit snapshots are included;
isolated user-data, saves, and account-bearing trees are excluded.

## Verification order

1. Unit-test parsing, command construction, state locking, dry-run, undo/redo,
   and JSON output.
2. Install the package in editable mode.
3. Resolve the installed console script, not an in-process shortcut.
4. Build the TEST-ONLY companion, ContractVerifier, and ComponentHost in
   Release with warnings as errors.
5. Run all tests from a cwd outside the repository with
   `CLI_ANYTHING_FORCE_INSTALLED=1`.
6. Run PoC 0/1/1b against a disposable 0.111 staging tree.
7. Run the opt-in `runtime_release` pytest gate from an unrelated cwd with
   `CLI_ANYTHING_FORCE_INSTALLED=1`,
   `STS2_HEADLESS_RUNTIME_RELEASE_GATE=1`, and an absolute external
   `STS2_HEADLESS_RUNTIME_PROFILE`. Release mode without the profile must fail.
8. Reverse-scan production build inputs and released DLL/PCK/ZIP artifacts for
   every bridge/protocol signature; require zero hits.
9. Run `git diff --check`, assert that broker/game/ComponentHost process count
   returned to zero on both success and failure paths, and confirm no runtime or
   build output is tracked.
