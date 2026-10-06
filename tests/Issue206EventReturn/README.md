# issue#206 event-option return regression

```text
dotnet run --project tests/Issue206EventReturn/Issue206EventReturn.csproj
python shin-getter-mod-godot/tools/validate_issue_206_event_return.py --source-root E:/Work/SlaytheSpare2
```

The entire actual production bridge file is linked without extraction; actual Harmony
FieldRef targets are exercised against test environment bindings. Godot/control/event/
session types are stand-ins, not a native game. The common return callback represents
completion, skip, local skip on save failure and restored-closed encounters; this does
not assert their persistence implementations anew.

Fixture input eligibility requires the native documented flags, including IsEnabled;
no ForceClick or test-side blanket Enable is used as a successful reward proof.
Tests preserve pre-disabled/locked states, exclude freed/off-tree/queued/replaced
nodes, protect native post-resume disable, and assert one return only. Real legal
mouse/controller input, reward choice/count and whole-run continuation must be retested
by the main task. Original failure evidence is not overwritten.
