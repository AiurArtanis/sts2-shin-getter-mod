---
name: cli-anything-slaythespare2-111-beta
description: Inspect, query, build, import, and launch the Slay the Spire 2 111-beta Godot/C# source tree through real CodeGraph, dotnet, Godot, and rg backends.
---

# Slay the Spire 2 111-beta CLI Harness

Use `cli-anything-slaythespare2-111-beta --help`. Put `--json` before the command
group for agent-readable output and use `--project-root` outside the source tree.

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

The harness wraps real `codegraph`, `rg`, `dotnet`, and Godot executables. It
does not edit decompiled C# source or fake DevConsole runtime execution. Session
configuration is locked, atomically auto-saved, and supports undo/redo. Preview
mutating operations with `--dry-run` where available.
