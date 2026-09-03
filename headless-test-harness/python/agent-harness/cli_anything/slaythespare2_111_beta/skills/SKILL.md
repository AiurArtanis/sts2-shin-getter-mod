---
name: cli-anything-slaythespare2-111-beta
description: Inspect the Slay the Spire 2 111-beta source tree and operate an isolated authenticated real-game headless test session through the installed CLI, broker, and TEST-ONLY companion.
---

# Slay the Spire 2 111-beta CLI Harness

Use `cli-anything-slaythespare2-111-beta --help`. Put `--json` before the command
group for agent-readable output. The selected game source is read-only; every
writable state, runtime, staging, and evidence path must be outside repositories,
Steam, Workshop, and production mod/deployment directories.

For live work, always pass `--project-root`, external `--state-dir`, external
`--runtime-root`, `--control-session`, and repeated `--protected-root` values for
every source, Steam, Workshop, production-mod, and production-deployment tree.
Each protected root must be an existing absolute directory; omission fails
closed with `E_ISOLATION_BREACH`. `--runtime-root` is the direct parent of the
named session directory.

## Commands

```powershell
cli-anything-slaythespare2-111-beta --json source status
cli-anything-slaythespare2-111-beta --json source files --glob "src/**/*.cs" --limit 100
cli-anything-slaythespare2-111-beta --json source search "CommandLineHelper.HasArg" --glob "*.cs"
cli-anything-slaythespare2-111-beta --json source args
cli-anything-slaythespare2-111-beta --json graph status
cli-anything-slaythespare2-111-beta --json graph explore CommandLineHelper HasArg
cli-anything-slaythespare2-111-beta --json build run --no-restore
cli-anything-slaythespare2-111-beta --json game doctor
cli-anything-slaythespare2-111-beta --json game import --dry-run
cli-anything-slaythespare2-111-beta --json console list
cli-anything-slaythespare2-111-beta --json console show win
cli-anything-slaythespare2-111-beta --json session status
```

Live command groups are:

```text
process start|list|status|stop
runtime handshake|connect|exec|dispatch|wait-event|wait-terminal|request-status
evidence finalize|verify
session close
```

Before mutation, verify the handshake's game version/commit, adapter ID/hash,
drivers, actual user-data path, and capabilities. Use only server-issued handles.
Never construct a handle, select by localized text, kill by process name, expose
the token, or adopt an old game process after broker failure.

Use `runtime exec` for terminal operations. For a card that may select, use this
exact flow:

```text
dispatch parent -> wait-event choice_required -> choice.select -> wait-terminal parent
```

Preserve the parent request ID, owner, choice generation, choice handle, and
candidate handles exactly. A stale continuation is expected to fail with
`E_STALE_HANDLE`; an unrelated mutation while the parent owns the lane is
expected to fail with `E_MUTATION_BUSY`.

Request `queue_settled` when asserting gameplay outcome. It means the exact
action completed, queues are empty, the executor is idle, no choice is pending,
the command-specific ready predicate holds, and a post-snapshot was captured.
It is not a fixed delay.

Reuse a request ID only with the entire same canonical request, including
`command`, `args`, `wait_for`, and `timeout_ms`. An exact in-flight duplicate
must not execute twice; an exact completed duplicate returns `replayed=true`;
changed content is `E_IDEMPOTENCY_CONFLICT`. Only the 256 newest terminal
payloads are replayable, but request-ID/digest tombstones live for the entire
companion process epoch. An exact retry after payload eviction returns
`E_IDEMPOTENCY_WINDOW_EXPIRED` and must never be converted into a fresh
mutation. Reconnect only within the server's reported rolling replay window.
Treat `E_RESUME_WINDOW_EXPIRED` and `E_OBSERVER_OVERFLOW` as invalid-case
outcomes, not as permission to guess or continue mutating.

For a real-runtime release decision, use the installed command from a cwd
outside the repository and opt in explicitly:

```powershell
$env:CLI_ANYTHING_FORCE_INSTALLED = '1'
$env:STS2_HEADLESS_RUNTIME_RELEASE_GATE = '1'
$env:STS2_HEADLESS_RUNTIME_PROFILE = '<absolute-external-profile.json>'
python -m pytest <absolute-tests-path>\test_runtime_release.py -v -s --tb=no
```

The external profile must use schema `sts2-runtime-release-profile/v1`, set
`stage_companion=true`, and keep all writable paths outside protected trees.
Release mode without a valid profile is a hard failure. The gate must rebuild
and stage the TEST-ONLY companion, verify real PoC 0/1/1b including reconnect
and idempotency, stop the exact game process with exit 0, finalize/verify
evidence, and leave no broker or game process behind.

End every live case with exact `process stop --instance <id>` followed by
`session close`. Finalized evidence is immutable and must pass `evidence verify`.

The harness wraps real `codegraph`, `rg`, `dotnet`, and Godot executables. It
does not edit decompiled C# source or fake DevConsole runtime execution. Session
configuration is locked, atomically auto-saved, and supports undo/redo. Preview
`graph sync`, `build restore/run`, `game import/smoke/launch`, and
`session configure/undo/redo` with `--dry-run` when needed. Live
broker/companion mutations have no dry-run mode; query first and use disposable
staging.
