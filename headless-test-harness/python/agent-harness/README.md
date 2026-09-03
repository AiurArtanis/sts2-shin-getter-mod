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
  '--json'
)

cli-anything-slaythespare2-111-beta @common process list
```

`--runtime-root` is the direct parent of the session directory; the example
above resolves the session at `<external-session-parent>\case-001`.

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

Run the command without a subcommand for the interactive REPL. See `SOP.md` and
`../../docs/implementation-v0.2.md` for lifecycle, safety, staging, and PoC
details.
