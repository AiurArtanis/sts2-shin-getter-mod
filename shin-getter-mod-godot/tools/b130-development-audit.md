# B1.3.0 Development Handoff

Initial handoff: 2026-10-01. Baseline corrected: 2026-10-02.
Single active branch: `ticket/balance-b1.3.0-20261001`.
Current base: formal release `mod-v1.2.2`, peeled commit `7286ae7f59e12f266b9d008ac2509f0ade648e93`.
The original `origin/main@b0f76260baba42d33952c28dff5fb66980e2efbc` handoff and its review are historical only; they are not approval of the migrated baseline.

## Scope and Status

- `issue#215`: gameplay and three-language text implemented; Dragon Shining Spark candidate connected. Star Slash's two dedicated animations remain **unfinished / missing materials**. The issue is not complete.
- `issue#142`: Fighting Spirit excludes only Vigor's additive contribution. Strength, Weak, Vulnerable and other existing modifiers remain active. Counter damage does not consume Vigor.
- `issue#141`: Ki's upgrade grants 4 Vigor instead of 3; base 2 Vigor, 1 Ki, energy, Spirit and Exhaust are unchanged.
- `issue#123`: Shift Strike prints 6 damage. Only the first hit's final `DamageResult.UnblockedDamage` is compared against fixed 6. Below 6, transform and attempt one follow-up against the original still-hittable target; no recursion or retargeting. Upgrade transforms before the first hit. Old Vigor/Regeneration/Armor rewards and unconditional final transformation removed.

Shining Spark uses the original play-count hook, scoped to this card instance and current Dragon form. One replay repeats the entire card, including self debuffs and Ki follow-ups; it is not a duplicated damage call.

Star Slash retains 22 base damage, 1/2 exhaust selections, the existing printed-value aggregation and 50-point stack cap. After all selected exhaust commands/hooks, the existing Getter1/Dragon-compatible form predicate grants **one** HotBlood stack before the main attack. Existing HotBlood rules remain unchanged. The old per-card Vigor reward is removed.

## Shining Spark Candidate

Input: `D:/Library/Pictures/杀戮尖塔2-素材/anim-sprite/视频动画/真盖塔龙/真盖塔龙-闪光爆裂.mp4`.

Full SHA-256: `14ce9ac65c00c8ab0a119947f4872018e75b1ce85b5b4c7fa7d6b0890ab17244`.

Decoded source: 960x960, 24fps, 145 frames, 6.041666667 seconds.
Selected 0-based source indices, ordered by runtime 0-based sequence index:

```text
0,9,14,18,22,26,30,34,40,44,47,50,53,54,56,58,59,60,61,64,70,80,88,94,100,106,112,120,124,128,132,136,140,144
```

| Phase | Sequence Index | Source Index |
| --- | ---: | ---: |
| Visible release | 12 | 53 |
| Discard complete | 18 | 61 |
| Charge hold | 26 | 112 |
| Dash peak | 31 | 136 |
| End hold | 33 | 144 |

The baked dropped axe is not duplicated. Sequence indices 18-33 remain weapon-free. Source video contains a stationary forward lean, not an actual translational rush or idle recovery; only the visible Dragon sprite receives runtime visual translation. Creature/team positions are not changed by this new path. Other forms retain their legacy animation path.

34 source PNGs are 720x720 RGBA. Runtime sheet: 6x6, 4320x4320, 34 valid cells and 2 empty cells. Lossless, no mipmaps, non-VRAM; loaded on demand within the existing two-action cache. Source frames remain outside the Godot/PCK import scope. Manifest and future PCK exclusion gate cover 1,404 source frames / 33 action sheets; no PCK was exported or checked in this task.

Audit: `tools/b130-shining-material.json` contains every frame hash, source mapping, alpha and provenance. Reproducer: `tools/extract_shining_spark_frames.py verify` (requires the documented read-only material pipeline).

QA directory: `E:/Work/StS2 Mods/_validation/b130-shining-20261001`.
Key files: `QA-review.md`, `selected_checker.jpg`, `selected_light.jpg`, `selected_dark.jpg`, `selected_source_timing.gif`, `chroma_detector_hit_crops.png`.

Development preview was inspected, not accepted as final gameplay footage. Unresolved visual items: upper-edge axe cropping during the raised-axe transition; axe shape/color drift; finger/cape changes; 39 detector-hit pixels (4 in the weapon-free segment); idle transition and actual battle-scale timing. Detector counts do not establish visual acceptability. No generated repaint, local weapon erase, or approved old-source RGB rewrite was performed.

## Phase and Cancellation Entrypoints

- `NShinGetterShiningSparkSequence.TryCreate(owner, target)`: resolves that Owner's visible Dragon sprite, rejects an already-owned sprite, adds one per-play VFX root.
- `PlayToImpact(intro, release)`: discard 0.4s, charge 1.0s and optional intro wait, rush 0.35s, impact hold 0.1s. Release voice starts with rush and does not delay impact until the line ends.
- `SGC_ShiningSpark.OnPlay`: awaits impact **before** starting `AttackCommand`, so built-in hit FX cannot fire before the awaited phase. Main attack and actual Ki random-target commands remain the only damage/RNG owners.
- `Recover()`: after main/Ki attacks, visual return and energy fade over 0.5s; no damage events.
- `_Process(delta)`: one phase clock for sprite frame, visual translation, silhouette and tails. Combat pause freezes the clock; Instant/non-interactive modes use the finite fallback. Normal/Fast use the same base visual durations.
- `Close()` / `_ExitTree()` / card `finally`: stop phase updates, restore the owned sprite position, release stage/end Tasks, free local VFX. Death, form/animation change, hidden ancestors and combat ending interrupt the sequence. Metadata ownership checks prevent late recovery/cleanup from rewriting a newer playback. Public idle initialization preserves manually controlled pause.

These are source-level contracts. Rendering, voice pause behavior and actual interruption timing still need target-line validation.

## Missing Star Slash Materials

No verified Getter1 or Dragon dedicated Star Slash clips, per-frame grip/axis data, or weapon mattes were available. `PlayLegacyAnimationToImpact` is intentionally named and documented as a fallback. It does **not** implement the planned growing ray axe, out-of-frame extension, synchronized weapon overlay or new two-handed downstroke. No invented anchors or fake dedicated resources were added. The animation part of `issue#215` must remain open.

## Historical Verification: 2026-10-01

Passed: `validate_b130_core.py` (487 source checks), `validate_b130_ultimates.py` (8 negative source variants rejected), `validate_b1_1_0.py`, `validate_issue_159.py`, `validate_issue_32.py`, `validate_issue_169.py`, `validate_issue_191.py`, `validate_issue_21_31.py`, all 33 sprite sheets and 4 idle resources via builder `--check`, and diff whitespace checks. Two focused source-review findings (manual pause reset, ancestor visibility) were fixed and guarded; the follow-up ownership-gate coverage gap was also addressed.

JSON: **44/45 passed; full-library gate FAILED**. Unchanged baseline `shin-getter-mod-godot/data/dialogues/jpn.json` has an extra `]` at line 122. Confirmed in `b0f76260`; integration prerequisite, not silently absorbed from another branch. Final repair ownership must be verified by the main developer against actual commits, not assumed from issue labels. All six changed locale JSONs and the new material audit parse successfully.

UID declarations: 570 checked, no duplicates. The new C# script has a UID sidecar. GodotSharp 4.5.1 API names used by the new sequence were checked against local documentation/reflection without starting Godot or compiling.

Residual: Harmony runtime binding was not executed. The FightingSpirit-only private `ValueProp` bit `1 << 30` is unused by the inspected base game, but compatibility with other mods' private bits is not established.

No DLL compile, Godot/game execution, PCK export/validation, PR, merge, initialization, deployment, release version change, or shared-directory overwrite occurred. The dirty root checkout and original game source were not modified. Main developer review and target-line combined verification remain required; all four issues stay open.

## Formal v1.2.2 Baseline Migration: 2026-10-02

Artanis requested one B1.3.0 branch based on the previously released v1.2.2. The old main and formal release diverged after v1.2.1; inheriting old main did not include v1.2.2's BGM release changes and did include `issue#206`.

- Preserved original submission `e2640d0d9dbecb72ebbefc33dd25046452536bb9` under recovery-only tag `archive/b1.3.0-pre-v1.2.2-20261002`, also pushed to origin. No existing release tag was moved.
- Migrated only that B1.3.0 commit with `git rebase --onto 7286ae7f59e12f266b9d008ac2509f0ade648e93 b0f76260baba42d33952c28dff5fb66980e2efbc ticket/balance-b1.3.0-20261001`. Kept the same active branch and worktree; no second development branch.
- Code migration commit: `c5d1eec08875a8b518c3d5d48b8ce4b550573187`. Its direct parent and merge-base with the formal tag are `7286ae7f59e12f266b9d008ac2509f0ade648e93`; formal-tag ancestor exit 0, old-main ancestor exit 1.
- No conflicts or manual source resolutions. Original and migrated stable patch IDs both equal `b1d2ef3fc7069ca8b9cd8de3650e82da28bc9b06`. Before this audit update, both patches touch the same 63 paths. Only `validate-mod-resources.gd` has a different final blob because it retains v1.2.2's resource list while adding the same Shining Spark entry and changing 1370 to 1404 source frames.
- Formal manifest remains `v1.2.2`. BGM audio, catalog/configuration, settings localization, release notes, update history, README and workshop release materials have no delta from the formal tag. No `issue#206` dialogue sources, runtime classes or unrelated feature commits were brought in.

Current-baseline static checks passed: `validate_b130_core.py` (483 source checks), `validate_b130_ultimates.py` (8 negative variants rejected), `validate_b1_1_0.py`, `validate_issue_159.py`, `validate_issue_32.py`, `validate_issue_169.py`, `validate_issue_191.py`, `validate_issue_21_31.py`, `validate_issue_193.py`, and `validate_issue_214.py --combined193`. The v1.2.2 BGM gates were not removed or weakened. Builder `--check` verified 33 sheets / 1404 source frames and all four idle resources. All 43 tracked JSONs parse; 310 UID sidecars have no duplicates. Diff whitespace checks passed.

The old 44/45 JSON failure does not apply to this baseline: the malformed Japanese dialogue belonged to the excluded old-main feature. No JSON repair or feature cherry-pick was performed. This is a new static result, not runtime acceptance.

All 35 behavioral acceptance items remain unchecked. Previous `e2640d0d` white-box approval is retained only as history; the migrated submission requires fresh independent review. `issue#215` remains unfinished with the same Star Slash material gap and Shining Spark visual concerns. No main/Beta/PR changes, builds, Godot/game execution, PCK work, initialization, deployment, release version change or shared-root modification occurred during migration.
