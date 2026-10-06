# issue#206 console delivery — 2026-10-05

## Scope and baseline

- Human request: one-word test command following voice-test naming, NPC/ALL target,
  read/reset/unlock progress, deterministic next conversation; main developer reviews
  and then creates Obsidian `33-命令行索引`.
- `sgs` is the existing voice-test command; new command is `sgd`.
- Target/base: latest `origin/main@b0f76260baba42d33952c28dff5fb66980e2efbc`.
- Branch: `feat/issue-206-dialogue-console-20261005`.
- Worktree: `E:/Work/StS2 Mods/_worktrees/ShinGetterMod-issue-206-console-20261005`.
- No B1.3.0/Star Slash candidate, Beta or uncommitted root-state input.

## Implementation

Public `AbstractConsoleCmd` is discovered by official109's mod subtype registry;
the existing command-prefix bridge additionally dispatches `sgd`. `IsNetworked` is
false, debug-command policy is unchanged, case-insensitive IDs/Chinese aliases and
argument completion are supported. Full syntax/semantics/examples:
`data/dialogues/README.md#2026-10-05-测试控制台sgd`.

Plans are validated before any write and mutate a copy under the original
profile/revision/file-lock/flush/atomic-replace transaction. Reset deletes only the
selected NPC's completion, legacy evidence, absence/result records, encounter/cue
snapshots and pending requests. Existing sidecars never re-import history. On first
edit without a sidecar, the existing conservative legacy migration preserves other
NPCs' acquaintance facts; reset removes only its targets. Status does not migrate. ALL is scoped to
compatible NPCs, shown explicitly in the result; it never manufactures fourth stages.

The optional schema-1 `DebugNextDialogues` dictionary persists requests across
restart. The next eligible new encounter checks it before normal theme selection,
then removes its entry in the same commit as the frozen multilingual snapshot.
Failed writes leave progress/request unchanged; same-room resume reads the snapshot.
An open-count guard uses no static scene reference, registers once and releases on
normal close and ExitTree. Active reading permits only status. Other characters,
multiplayer, daily/custom, replay/non-interactive and unsaved runs cannot edit solo
progress. Menu edits target only the current profile. No original run history,
gameplay RNG, event rewards, character power or voice consumer is written by `sgd`.

## Existing baseline defects (not silent scope changes)

- Initial static gate stopped at malformed **jpn.json**, not zhs.json. Raw-decode
  verified119 entries in each language and exactly one extra Japanese closing `]`.
  Earlier provisional zhs/locale-option diagnosis was corrected after targeted reads.
- Main developer approved existing `eb174b823fa9d9e479b8c6b8c8cba6986af9de9e` one-line
  correction; cherry-picked as `b3caf275` (no dialogue content change).
- Main also approved `1006222807afa627afd930900891bed5056d80d7` RNG/nullable semantics.
  Only the necessary explicit cryptographic RNG alias is included; this new selection
  path already supplies nonnull flow and builds warning-free. Do not duplicate the
  unrelated whole cherry-pick over the new Session changes.
- All63 localized options exist. Ordinary translated rows intentionally contain less
  metadata than the Chinese source. No speculative metadata/text rewrite was made.

## Verification and boundaries

- RED: `validate_issue_206_console.py` failed with `sgd command has not been implemented`.
- GREEN: new wiring guard; original issue#206 guard with official109 source-root;
  issue#21/#31 voice guard; issue#192 persistence guard.
- `dotnet run --project tests/Issue206Console/Issue206Console.csproj`:832 assertions
  PASS. Links full production parser/plan/DTO/catalogue/session/command source,
  real multilingual resources and actual Harmony FieldRef; only game/Godot environment
  bindings are fixtures. Runs real file transactions in unique temporary profiles.
  Tests pending selection/consumption,84 NPC/pilot/stage combinations plus invalid inputs,
  NPC-local/ALL reset, file lock/retry, profile switches, corrupt input, old schema,
  deterministic win/loss/return/chat/variants, guard errors and completions.
- Formal109 isolated developer build:0 warning/0 error.
- 44 tracked JSON parsed;BAD=0. `git diff --check` PASS.
- No production-DLL/native Godot/UI input/real-save acceptance claimed. No game
  automation, Godot launch, PCK, initialization, shared deployment, PR, merge,
  Tag/Release or GitHub issue closure. Existing gameplay acceptance remains untested.

The command **changes the current test profile** and is not a nonmutating preview.
Operational examples and all parameter limits are part of the main-review handoff.

## 2026-10-06 minimal corrections after native test

Tested parent: `73e5bdb28dad9a8fe9aff36261d5d26647d7102e`. Main's official109
isolated report at `E:/StS2-Automation-Sandboxes/issue206-109-20261006/evidence/REPORT.md`
records180 native cases/174PASS/6FAIL/8527 assertions, not overall acceptance.
Five failures were the invalid Architect example; one was consecutive-space input.
The supplementary TEST-ONLY Architect scene passed its five themes without changing
those original failure records. This correction does not rerun or relabel them.

1. Production change is confined to the `sgd` branch of the existing prefix:
   discard whitespace-only tokens and trim surviving tokens before dispatch. The
   native public console already trims the input, but this prefix executes before
   native argument filtering. Other mod branches retain their original arrays;
   export_cards/event/unknown command paths pass through unchanged.
2. README removes the runnable `event THE_ARCHITECT` example. Official109's event
   inventory is AllEvents+AllAncients and excludes this ending. Set `sgd`'s request
   before following the actual ending; explicit native TheArchitect/EventRoom
   construction belongs only to a TEST-ONLY isolated fixture, not a shipped command.
   No new production command or scene builder was added.

RED: after correcting a fixture-only top-level C# variable naming collision, the
linked **production Prefix** failed `sgd  TANX  status` with Unknown NPC, reproducing
the native finding. GREEN:852 managed assertions, including normal/multiple-space/
leading-trailing inputs, padded tokens, clear/unlock semantics, unknown NPC/operation/
bad stages, raw quoted-array preservation for four other commands and export_cards
passthrough. Game environment/tokenizer are fixtures; this is not native NDevConsole.
The structural gate locks normalization to the sgd block and prevents the invalid
Architect example from returning. Original issue206, issue21/31 and issue192 guards,
44 tracked JSON/BAD0 and diff-check PASS; official109 developer build0 warnings/errors.

Only offline validation here. Main must rerun the failed native inputs and corrected
Architect test routing. No game/Godot launch, PCK, initialization, shared deployment,
PR/merge/release/issue closure; no issue124/215/B1.3.0, root or Beta changes. The
remaining multiplayer, old-history, natural priority, cue-SL, rewards/score and human
coverage gaps in the original report remain unproven.
