# Slay the Spire 2 111-beta Harness SOP

## Purpose

This harness gives agents a deterministic command-line surface over the real
111-beta source tree. It does not reimplement game behavior in Python.

## Real backends

- Source inspection uses the files under the selected project root and `rg`.
- Semantic inspection calls the repository's existing `codegraph` index.
- Builds call `dotnet` against `sts2.csproj`.
- Import and launch commands call a Godot 4.5 Mono executable.
- DevConsole commands are indexed from their C# definitions. They are not
  executed outside the live game because their runtime state is unavailable.

## Command surface

- `source status|files|search|args`
- `graph status|sync|query|explore`
- `build status|restore|run`
- `game doctor|import|launch|smoke`
- `console list|search|show`
- `session status|configure|undo|redo`

Every command accepts the root-level `--json` option. Mutating operations expose
`--dry-run` and report the exact backend command or state change before writing.

## State and safety

Harness state lives in `agent-harness/.state` by default. Configuration writes
are protected by an inter-process lock, saved atomically, and recorded in a
bounded undo/redo history. Source files are read-only from the harness.

`graph sync`, `build`, and Godot import/launch can update backend-owned caches or
build outputs. They never edit decompiled C# source. `game launch --detach` is
the only command that intentionally leaves a process running.

## Verification order

1. Unit-test parsing, command construction, state locking, dry-run, undo/redo,
   and JSON output.
2. Install the package in editable mode.
3. Resolve the installed console script, not an in-process shortcut.
4. Run a real CodeGraph status and explore query.
5. Run a real `dotnet build` of `sts2.csproj`.
6. Confirm Godot discovery and static DevConsole indexing.
