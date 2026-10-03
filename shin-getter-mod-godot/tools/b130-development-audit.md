# B1.3.0 Development Handoff

Initial handoff: 2026-10-01. Baseline corrected: 2026-10-02.
Single active branch: `ticket/balance-b1.3.0-20261001`.
Latest feedback revision: 2026-10-02, based on `ac2e7425e0c930850f57283f6ab48093dadb1f18`.
The feedback revision below supersedes the initial replay reward and visual timings; earlier delivery and migration records remain historical evidence.
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

## Human Feedback Revision 3: 2026-10-02

Artanis completed the prior manual pass: 32 of the 35 top-level checks were checked, three failed checks and their nested observations remained. Those historical results are preserved in the Obsidian ticket; no agent resets or checks them. New behavior checks are a separate feedback-3 checklist.

- `issue#215`: remove this card's `ModifyCardPlayCount` replay. At the beginning of Shining Spark's OnPlay, the current Dragon form gains 3 Ki before Vulnerable/Frail, main damage and current-Ki random hits. Keep 2 energy, 11/14 main damage, 6/9 Ki damage. All three languages use `[getter_ray]` for the Dragon reward name. The detached-combat follow-up is skipped instead of passing a nullable combat state.
- Spark's speech bubble has a per-bubble child follower anchored to the actual visible sprite, transformed through viewport/canvas space. Only the Spark line's bubble is scaled 1.3 once, including its contents. Generation-based voice cleanup stays in the voice service; sprite exit, invisibility, death or combat ending hides the follower bubble. Other subtitles are unchanged.
- Adopted the main developer's read-only VFX recommendations: discard 0.4s; charge at least 1s with up to 30 display-pixel recoil in the last 0.35s; hold until the intro ends; release voice at rush start; cubic ease-in rush 0.28s; impact 0.08s; smooth visual recovery 0.35s. Normal/Fast share this sequence clock. Independent green/white outer arcs, low-alpha additive silhouette and five historical frame/transform snapshots at 40ms intervals surround the body. Tails decay in 0.16s, shorten with travel distance, and stop sampling outside rush. No global time scale, shader TIME, gameplay RNG, source-image repaint or logical creature displacement.
- Star Slash emits one buff-independent Hot Blood icon flash after all selected exhaust commands/hooks and before the existing form-gated Hot Blood application. The flash uses the canonical power's icon texture only; it does not manufacture/apply a temporary power or alter stacks. The prior dedicated-animation material gap remains, regardless of the historical manual animation checkmarks.
- `issue#142`: the card now uses static `CounterDamage` 5/8, not DamageVar's powered preview. Multiple cards already stack one power's cumulative Amount in the inspected original source; the feedback is not proven to be a stacking failure. The original incoming-damage hook is awaited before block/HP loss, and the old late hook normally should cancel a counter kill. Added hostile-source/reentry guards, counter-kill provenance and cancellation before redirection plus a narrowly scoped final HP-loss postfix. Nonlethal hits remain unchanged; Strength/Weak/Vulnerable remain active, Vigor adds nothing and is not consumed. The reported first-hit scene was not reproduced in this static-only task; runtime confirmation and Harmony binding remain required.
- `issue#123`: both attacks use one complete normal-speed animation helper. Actual frame-duration/progress polling awaits the midpoint for the real AttackCommand, then awaits recovery before returning results. The first results/fixed-6 threshold are inspected only after the complete first animation; transformation completes next, then a complete follow-up hits only the still-valid original target. Noninteractive, Instant, interruption, death, exit, combat ending and stalled-animation paths do not wait on an unconditional AnimationFinished signal. No upgrade, Seal, Dragon, target, threshold or recursion rule changes.

Static-only checks passed: `validate_b130_core.py` (487 source checks), `validate_b130_ultimates.py`, `validate_b130_fighting_feedback.py` (10 tests), `validate_b130_feedback.py`, `validate_b1_1_0.py`, `validate_issue_159.py`, `validate_issue_32.py`, `validate_issue_169.py`, `validate_issue_191.py`, `validate_issue_21_31.py`, `validate_issue_193.py`, and `validate_issue_214.py --combined193`. New gates include 11 counter-negative mutations and 11 visual-negative mutations; the ultimate gate retains 8 negative mutations. The current ultimate gate verifies unchanged 34-frame source hashes and sheet pixels; unchanged remaining sheets reuse the prior full-sheet audit rather than claiming a rerun. All 43 tracked JSONs parse; 310 tracked UID sidecars have no duplicates. Diff whitespace checks passed. Full rendering, target-line build, actual damage, multiplayer and human visual acceptance are not claimed by these checks.

GodotSharp 4.5.1 API names were read from the installed XML documentation; no compiler or Godot process was started. Manifest remains `v1.2.2`; the formal baseline, BGM, release assets, frame/sheet sources and old-main exclusion remain unchanged. Development happens only in the original isolated worktree. No main/Beta/pr, PCK, initialization, deployment, public-release or shared-root change in this revision. Independent review is required before target-line integration/testing; all four issues remain open.

## Independent Review Repair 4: 2026-10-02

Main developer review of `bd706e9a` requested one P2 correction: the shared state machine's TryPlay returns true both for a newly started action and for protection of a previous special action. Shift Strike could therefore read an old action past its midpoint as its own complete first attack. The four passing gates did not cover that branch; the review was not approved.

The complete-attack helper now waits for the previous protected special action, without using its old frame/progress as the new attack. The wait has a six-second active-time budget, freezes during combat pause and exits on hidden/invalid/exited sprite, death, combat ending or Instant. It neither stops the old action nor forcibly switches it to idle. A stalled/invalid visual follows the existing no-animation command fallback instead of hanging gameplay.

After the old action ceases to be protected, the helper requests a fresh attack through the state machine, checks the actual animation name and playing state, resets both frame and progress to zero, then waits for this new action's midpoint and recovery. The existing public TryPlay protection contract remains unchanged for all other cards. The old-action predicate is shared with its actual suppression rule, not a duplicated list of animation names.

Focused source checks passed: B1.3.0 core 487, feedback 18 negative variants (the original 11 plus seven review regressions), ultimate eight negatives with unchanged 34-frame source/sheet verification, and issue#32. New regressions cover an old protected action past the midpoint, idle transition before the fresh request, explicit Attack/zero-progress confirmation, timeout and death/exit interruption. These are source contracts, not a simulated or observed game reproduction. The earlier 43 JSON / 310 UID and unchanged counter checks are reused because those files have no delta in this repair.

This repair requires a new independent review. It does not authorize or perform compilation, game/Godot execution, merge, PCK work or deployment. Original human checkmarks and the 29 feedback-3 behavioral checks are retained; review evidence is not inserted into the player checklist.

## New Shining Spark Clip Integration 5: 2026-10-02

This revision continues `ticket/balance-b1.3.0-20261001` from `f51059b27fb542d7bafae3c489a31a0530acf3b2`, still based on formal `mod-v1.2.2@7286ae7f59e12f266b9d008ac2509f0ade648e93`. It does not merge old main or `issue#206`. The release manifest remains `v1.2.2`; gameplay values, all other cards and the voice service have no delta.

The user explicitly accepted the new video's screen-facing charge, followed by rightward airborne propulsion. The main developer delivered the retimed frames and phase map. Source SHA-256: `d56ad4479bccb9b576d6eda73899403f36111b69a250f68a1d43041cd1851c88`. Only source timestamps `0 <= t < 5.4` are used; the last selected timestamp is 5.375s. The external MP4 and 130 full-size mother PNGs are read-only.

47 delivered 720x720 RGBA PNGs are copied byte-for-byte from the action's `sprites_import`, together with `stage_timing_map.json` and `sampled_frame_map.json`. No re-keying, resizing, interpolation, repaint, reverse playback or extra poses. The source maps remain outside the Godot import scope. Runtime sheet is 8x6 / 5760x4320, 47 populated cells and one all-zero cell. Lossless, no mipmaps, non-VRAM and the same two-action loading cache. Source inventory is now 1417 frames / 33 sheets; the future PCK exclusion assertion is synchronized but PCK has not been exported or checked.

| Stage | Zero-Based Frames | Budget |
| --- | --- | --- |
| Discard | 0-11 | 0.4s |
| Charge (including one baked jump) | 12-35 | At least 1.0s |
| Rush | 36-44 | 0.28s |
| Impact | 45-46 | 0.08s |
| Recover (hold final airborne pose) | 46 | 0.35s after real damage commands |

Stages use disjoint ranges and `floor(frame_count * progress)` with endpoint clamping, so each delivered frame receives the map's equal hold duration. SpriteFrames' registered 30fps is not used as a replacement for this paused, manually advanced clock. Minimum phase budget remains 2.11s; actual duration adds Shining's extra hold and real main/Ki damage command time. Shining starts at charge entry, may extend frame35; Spark starts at rush entry and is not awaited before impact. Silent/consumed cues, pause, Normal/Fast, Instant/fallback, ownership, hidden/death/exit/form-change and combat-end boundaries are preserved.

The new PNGs already contain the jump and green body arcs. No root jump is added. The duplicate current-body additive silhouette and procedural body arcs are removed. Runtime retains only the existing bounded recoil/cubic lunge/smooth return, five actual-frame historical tails with 40ms sampling / 160ms decay, and the brief impact ring. Logical Creature/team positions and gameplay RNG are untouched. Spark's follower/1.3 scaling and all voice claim rules are unchanged.

Current reproducibility command: `python -B tools/import_shining_spark_frames.py verify`. The earlier 34-frame extractor and audit sections above are historical, not the current input or verification command. `b130-shining-material.json` now records delivered PNG hashes, raw delivery map hashes, portable canonical-JSON content hashes, phase ranges and provenance. Content hashes prevent Git's line-ending normalization from causing false provenance failures in another checkout. The obsolete extractor refuses the new output inventory rather than replacing it with 34 frames.

Recovery's final-frame hold runs inside the guarded Stage callback. Stage also checks the current animation before its first update, preventing a newer/death animation from being overwritten between damage completion and the next process tick.

Static checks passed: core 487 source checks, ultimate 19 negative variants and all 47 exact RGBA cells, visual feedback 18 negative variants, B1.1.0, issue#159, issue#32 and issue#21/issue#31. Builder `--check` reran all 33 sheets and four idle resources. All 45 tracked/new JSON files parse, including the two new maps. Diff whitespace checks pass. issue#159 initially failed only its historical 34-frame count; this action's approved aggregate digest, count and PCK exclusion count were updated, without weakening other actions' guards. Runtime rendering, real voice timing, actual combat and target-line compilation remain untested.

Residual visual check: impact compresses source frames104 to130 into 80ms, so the baked green energy disappears quickly; no smooth fade is claimed. Final airborne pose is held during root recovery before returning to idle, with no invented landing. Main developer independent review and game testing must judge impact/recovery continuity. Star Slash's two-form dedicated material/weapon-overlay gap remains unresolved; B1.3.0 overall stays in development and all four issues stay open.

No PR, merge, DLL compile, Godot/game startup, PCK work, initialization, deployment or shared-directory overwrite. Historical human checkmarks and prior feedback3/4 lists are retained. New direct behavior checks belong only in feedback5; engineering evidence remains in development feedback.

## Human Visual Feedback Revision 6: 2026-10-03

Artanis has checked all feedback4/5 items and the latest Fighting Spirit checks. Feedback3 still requests three visual corrections: bubbles must clear the airborne body, Spark text must be another 50 percent larger, and green energy must wrap the Dragon coherently. Historical observations and checkmarks are retained; this revision does not alter previously accepted gameplay or 47-frame timing.

- Read both attached screenshots and the animation research document, and requested the main developer's read-only advice. Adopted its behind-body soft shell/narrow edge and actual-content bubble-clearance approach. No new mother PNGs, re-keying, color edits, gameplay values, source video changes or Star Slash materials.
- Both Shining and Spark use the same per-bubble follower during the dedicated Dragon action. Spark's prior 1.3 scale is multiplied by 1.5 once, yielding 1.95 relative to the original bubble; Shining remains unscaled. Attachment is idempotent. Owner/death/exit/ancestor visibility and the existing voice generation/claim lifetime remain intact.
- Offline alpha>=128 bounds for all 47 source frames account for the jump inside the PNG. Current-frame/flipped/centered/offset rectangles project all four corners through canvas space; idle and other animations revert to the current texture rectangle. Bubble/Shadow/Text visual rectangles are combined, including the original bubble's transforms. The bottom is kept 24 viewport units above the body; top overflow chooses the roomier side rather than clamping back onto the head. The follower runs after the sequence and stays hidden until its first positioning pass.
- Each sequence owns one Polygon2D and ShaderMaterial behind its actual sprite. The 28-source-pixel padded quad explicitly samples the current atlas Region; out-of-cell samples return zero and valid bilinear footprints stay inside the current 720 cell. Mirroring is applied once. Alpha dilation forms a 6/10-pixel narrow edge (0.28 alpha factor) and 18/24-pixel soft halo (0.12 factor), excluding high-alpha armor. There is no duplicate RGB body, detached arc or global shader TIME.
- The shell uses the existing pause-aware clock: off during discard, smooth 0->0.65 during charge, retained during additional Shining hold, 1.0 in rush, 0.95 through impact/damage, then fades over the unchanged 0.35s recovery. Closing, interruption or lost ownership removes this sequence's sprite-child shell. Source-frame green fading is not used as the shell's intensity clock.
- Static verification: core 487, feedback 26 negative source variants, ultimates 19 negatives and all 47 exact RGBA cells, visual-revision6 47 high-alpha bounds/1128 AABB arithmetic cases/752 atlas-footprint cases/four negative shader variants, B1.1.0, issue#159, issue#32, issue#21/issue#31, issue#193 and issue#214 combined193 passed. All 45 tracked JSONs parse; 311 UID declarations have no duplicates; whitespace checks pass. Geometry arithmetic and shader source contracts are not C#/Godot rendering or pixel acceptance tests.
- The prior blanket no-shader gate was replaced by behind-body/no-armor-repaint/current-region/padding/cleanup guards to match the new human request; the 47-frame stage, ownership, pause and mutation gates remain. The new shader is added to the future PCK resource-load list. No compiler or Godot/game process was started; shader compilation, real text layout, clipping at unusual viewports, visual strength and performance remain review/runtime prerequisites.

Development continues only on the original isolated branch from c82bfca7, based on formal mod-v1.2.2. No old-main/issue#206, shared root, installed artifacts, release version, main/Beta/pr or public-release changes. Submit this focused correction for independent main-developer review. Star Slash's dedicated two-form clip/weapon-overlay gap is unchanged, and the whole B1.3.0 batch is not marked complete.
