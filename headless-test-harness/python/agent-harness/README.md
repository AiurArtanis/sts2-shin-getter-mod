# cli-anything: Slay the Spire 2 111-beta

Agent-native CLI harness for inspecting the read-only 111-beta Godot/C# project
and controlling an isolated real-game TEST-ONLY runtime. v0.2 keeps the legacy
source/CodeGraph/build/Godot surface and adds a long-lived broker, exact process
ownership, authenticated companion transport, state snapshots, typed card play,
choice continuation, and immutable evidence verification.

## Install

```powershell
cd <ShinGetterMod-worktree>\headless-test-harness\python\agent-harness
python -m pip install -e ".[test]"
```

Release validation must resolve the installed console script. Do not replace it
with an in-process import.

## Common options

Live commands require an external session-parent directory and a session ID:

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

cli-anything-slaythespare2-111-beta @common process list
```

`--runtime-root` is the direct parent of the session directory; the example
above resolves the session at `<external-session-parent>\case-001`.
Every `--protected-root` must be an existing absolute directory. Live commands
fail with `E_ISOLATION_BREACH` when no explicit protection set is supplied;
repeat the option until every source/deployment tree in the environment is
covered.

## v0.2 command groups

- `process start|list|status|stop`
- `runtime handshake|connect|exec|dispatch|wait-event|wait-terminal|request-status`
- `evidence finalize|verify`
- `session close`
- Legacy: `source`, `graph`, `build`, `game`, `console`, and session
  configuration/undo/redo commands

Use `runtime exec` for a terminal request. Use
`dispatch -> wait-event(choice_required) -> choice.select -> wait-terminal`
when a card may require a selection. Always pass server-issued handles back
unchanged; do not construct handles or infer candidates from display text.

Retries must preserve the entire canonical request, including `command`,
`args`, `wait_for`, and `timeout_ms`, while reusing the request ID. Volatile
transport fields such as sequence, connection ID, and wall time are excluded.
An exact in-flight retry attaches to the original work, an exact terminal retry
returns `replayed=true` while its terminal payload is among the 256 most recent,
and any content change returns `E_IDEMPOTENCY_CONFLICT`. The request ID/digest
tombstone lives for the whole companion process epoch. If its terminal payload
has aged out, the exact retry returns `E_IDEMPOTENCY_WINDOW_EXPIRED` and is not
executed again.

`--dry-run` is implemented only by `graph sync`, `build restore`, `build run`,
`game import`, `game smoke`, `game launch`, `session configure`, `session undo`,
and `session redo`. Broker/companion live mutations do not advertise a dry run;
inspect with status/query commands and use disposable staging instead.

## Real-runtime release gate

Keep the profile, staging tree, runtime sessions, and settings template outside
all repositories and protected deployment roots. Then run from an unrelated
cwd with the installed console script available:

```powershell
$env:CLI_ANYTHING_FORCE_INSTALLED = '1'
$env:STS2_HEADLESS_RUNTIME_RELEASE_GATE = '1'
$env:STS2_HEADLESS_RUNTIME_PROFILE = '<absolute-external-profile.json>'
python -m pytest <absolute-tests-path>\test_runtime_release.py -v -s --tb=no
```

The profile schema is `sts2-runtime-release-profile/v1` and must explicitly set
`stage_companion=true`. With release mode enabled, an absent or invalid profile
is a test error rather than a skip. The gate rebuilds and stages the companion,
checks authenticated fingerprints, real no-choice and choice actions,
disconnect/reconnect, duplicate/conflict/replay behavior, graceful exit 0, and
finalized evidence verification.

Run the command without a subcommand for the interactive REPL. See `SOP.md` and
`../../docs/implementation-v0.2.md` for lifecycle, safety, staging, and PoC
details.
