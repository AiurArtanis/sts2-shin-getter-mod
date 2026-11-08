# issue#206 console managed regression

```powershell
dotnet run --project tests/Issue206Console/Issue206Console.csproj
python shin-getter-mod-godot/tools/validate_issue_206_console.py
python shin-getter-mod-godot/tools/validate_issue_206.py --source-root E:/Work/SlaytheSpare2
```

The managed executable links the actual production parser/mutation plan, save DTO,
catalogue and complete session source. Only Godot/game environment bindings are
fixtures; Harmony's production `FieldRef` implementation is referenced directly.
The real three-language JSON resources are embedded without rewriting them.
It is **not** a production-DLL, native Godot, UI/input or gameplay test.

All persistence uses a newly generated `shin-getter-issue206-*` directory below the
user's temporary directory, printed on launch and retained for inspection. It never
reads or writes the real save account/profile, deployment or original game source.
Tests cover argument rejection, 7 NPC × 3 pilot × 4 progress levels, aliases, ALL,
deterministic encounter selection, once-only consumption, same-room resume,
file-lock failure/retry, profile isolation, old schema-1 compatibility and damaged
file preservation. Failure is a nonzero exit; assertions do not imply game acceptance.

2026-10-06 corrections additionally link and invoke the actual production Harmony
prefix via reflection. The public-input tokenizer boundary is a fixture matching
official109's `Trim().Split(' ')`, **not** a native NDevConsole test. Consecutive spaces,
leading/trailing input whitespace, padded argument tokens, ordinary inputs, unknown
NPC/operation and empty inputs are covered. Other mod command stand-ins capture the
same original array (including quotes); `export_cards`/event/unknown commands must
pass through untouched. The original native failure must be rerun by the main task.

2026-10-10 release baseline: the suite also seeds a full pre-release/test sidecar
and checks all nine NPCs start at their common first meeting with zero stages and
no inherited old acquaintance, snapshot/cue, result or debug request. Read-only
status writes nothing; a transaction-lock failure preserves original bytes; first
successful initialization records the fixed v1.3.0 epoch and an exact independent
backup. A new session retains progress earned afterward, other profiles remain
independent, and an unknown future epoch is not overwritten. This replaces the
old unpublished acquaintance-import expectation, not the history of that test.
