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
`--runtime-root`, and `--control-session`. `--runtime-root` is the direct parent
of the named session directory.

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

End every live case with exact `process stop --instance <id>` followed by
`session close`. Finalized evidence is immutable and must pass `evidence verify`.

The harness wraps real `codegraph`, `rg`, `dotnet`, and Godot executables. It
does not edit decompiled C# source or fake DevConsole runtime execution. Session
configuration is locked, atomically auto-saved, and supports undo/redo. Preview
mutating operations with `--dry-run` where available.
