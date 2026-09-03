# cli-anything: Slay the Spire 2 111-beta

Agent-native CLI harness for inspecting, building, importing, and launching the
decompiled 111-beta Godot/C# project at the parent directory.

## Install

```powershell
cd E:\Work\SlaytheSpare2-111-beta\agent-harness
python -m pip install -e .
```

## Examples

```powershell
cli-anything-slaythespare2-111-beta --json source status
cli-anything-slaythespare2-111-beta graph explore CommandLineHelper HasArg
cli-anything-slaythespare2-111-beta build run --no-restore
cli-anything-slaythespare2-111-beta game doctor
cli-anything-slaythespare2-111-beta console search relic
```

Run the command without a subcommand for the interactive REPL. See `SOP.md` for
backend and safety boundaries.
