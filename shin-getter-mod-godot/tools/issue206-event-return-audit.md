# issue#206 original-event input return correction — 2026-10-07

## Exact baseline and failure

Development starts at integrated main `679182905ed2855f7928cb88cc581e23079b0660`
(pr#244). Branch `fix/issue-206-event-return-20261007`; no older feature/B1 branch
merge. Main's genuine official109 standard saved solo run stalled in the initial
Neow room:3 options visible/unlocked/MouseFilter.Stop, but IsEnabled=false after
the bond dialogue. game1 timed out waiting for enabled choices; game2 diagnosed
the same state. Only1 room was reached, not a completed run or50-room pass.

Official109 source establishes:

- NEventLayout.DisableEventOptions calls inherited Disable() on each option.
- NClickableControl.Disable sets _isEnabled=false and disables focus/input.
- NEventOptionButton.EnableButton only restores MouseFilter.Stop, never Enable().
- Mouse press/release/focus require enabled/visible flags. ForceClick directly
  invokes OnRelease and emits Released; it is not legal-input eligibility proof.
- Option.IsLocked is still rejected by native OnRelease and OptionButtonClicked.

Prior193 dialogue cases,17 directed rechecks and522 image checks retain their
original coverage; none supplies the missing original reward-input/count proof.

## Minimal production boundary

Only ShinGetterBondDialoguePatch.cs changes. A scene-local snapshot captures the
option instances that are enabled immediately before the bridge disables them.
At its existing once-only return callback, after marking Returned but **before**
native resume, inherited Enable() is called only for snapshot nodes that remain
valid, in-tree, not queued for deletion and in the layout's current OptionButtons.
Invalid/off-tree/queued layouts do not continue.

The original content visibility is restored; native resume still controls mouse
filters, animation, locking and any subsequent disable. No global EnableButton
patch, blanket enable-after-resume, new reward callback, direct reward grant,
ChooseLocalOption or test-side ForceClick/Enable workaround. Pre-disabled options
are not owned by this layer; locked options retain native lock guards. Multiplayer
eligibility/voting is unchanged. Session persistence/skip/SL, dialogue/voice/sgd,
artwork, B1 and issue#243 are untouched.

The snapshot belongs to the existing scene callback, not a new static node cache.
The UI already clears that callback on close/ExitTree. The Returned guard remains
before restoration/resume, so duplicate UI return does not repeat the operation.

## Development validation

```text
dotnet run --project tests/Issue206EventReturn/Issue206EventReturn.csproj
python shin-getter-mod-godot/tools/validate_issue_206_event_return.py --source-root <official109-source>
dotnet run --project tests/Issue206Console/Issue206Console.csproj
```

- Structural RED: missing enabled-state snapshot on the original bridge.
- Linked-production bridge RED: original EnableButton-only return leaves options
  disabled. The whole actual bridge is linked; Harmony FieldRef is real, game/Godot
  types are environment fixtures. Initial unused fixture members were corrected
  without suppressing diagnostics.
- GREEN:40 bridge fixture assertions. Common callback routes for complete/skip/
  save-failure local skip/closed-encounter restore; original disabled/locked states;
  freed/off-tree/queued/replaced buttons; invalid/queued layouts; native post-resume
  disable; duplicate return/already-returned path. Input eligibility in the fixture
  requires enabled flags and never treats forced callback as native reward proof.
- Formal109 developer build0 warning/0 error; original issue206 + official-source
  probes, console and priority gates PASS. Existing linked console suite852 PASS.
- Integrated compact-art gate PASS as unchanged-resource assurance, not another
  issue237 image acceptance run.56 tracked JSON parse/BAD0; diff-check PASS.

No native Godot/game run, PCK/export/initialization, shared deployment, PR/merge,
release or issue closure. Root b2f01685 and user state/deployed files are preserved.
Main must review the exact new HEAD and repeat original legal-input reward choice
conditions before continuing its full-run target from a new final main.
