# Saint Dragon Audit Refresh

This audit-data-only follow-up records the output of the existing
`audit_issue_93_codegraph.py`; it does not modify additional business code.

## Current Naming and Migration Delta

- Card model rename and deterministic reordering of its existing override entries.
- Updated hashes of the seven renamed/referencing existing production files.
- One new migration source file, three Harmony constructor/lookup patches,
  and the Exists target name.
- Updated source locations and semantic-reference hash; no new game API candidate.

## Pre-Existing Baseline Corrections

The checked-in inventory predates parts of baseline
`e64b6ea400c367d9aae2e59cb655df639d5d3daa`. The main development task explicitly
authorized correcting these known stale records on 2026-09-09:

- `f08f6e23` (issue#191, pr#218) had already removed four local voice SavedProperty
  attributes in each of SGR_EmperorsFragment and SGR_GetterFurnace. Their inventory
  hashes and line counts are corrected, without changing either source file.
- `e64b6ea4` (issue#216, pr#219) had already added the PowerUi `_model` field access
  and null gates. The source hash, +11 lines, one reflection call, target name and
  subsequent line positions are corrected, without changing that source file.

`git diff e64b6ea --` for those three business files is empty (exit 0).
This is known baseline inventory debt, not source-tree/environment drift.

## Verification

- Correct 111 sts2.dll reference SHA-256:
  `6896BBA91CEDDC661B3F789749E9F0AAC338F5DDBBB92C598FC344DEC822DC19`.
- Isolated production compilation: 0 warnings / 0 errors.
- Existing audit generator followed by `--check`: PASS.
- Source inventory: 325 -> 326 files, 28336 -> 28388 lines,
  121 -> 125 dynamic calls, 89 -> 91 target names.
- The added 52 lines comprise this migration's 49 and baseline corrections' net 3.
- 26 changed API candidates, 378 game types, 699 direct game members and
  33 generic game members remain unchanged; original game index totals unchanged.
- No Beta component/game execution, Godot process, PCK, deployment or release.
