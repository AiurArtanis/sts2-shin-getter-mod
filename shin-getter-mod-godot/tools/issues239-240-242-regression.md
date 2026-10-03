# issue#239, issue#240, issue#242 regression notes

## Scope and baseline

- Base: `c82bfca7a44345bd28c3c6d2c7c26cf9ab5dc22b` on the authorized B1.3.0 test line.
- Branch: `ticket/bugfix-239-240-242-243-b130-20261003`.
- Formal 109 only. No Beta changes, compilation, PCK export, game launch or deployment.
- The branch name includes issue#243, but that issue is investigation-only and has no production fix in this delivery.
- The target advanced to `7999cfd0` during development. The main development session explicitly confirmed keeping the original base without rebasing or merging the separate visual revision.

## issue#239: event icon ordering

The event option icon explicitly used ZIndex 8. In the original `scenes/run.tscn`, the room precedes GlobalUi; GlobalUi has `z_as_relative = false`, and the overlay container has no higher explicit ZIndex. A positive icon ZIndex can therefore draw above a later selection overlay instead of following its event button.

The fix only changes the icon ZIndex to 0. Mouse transparency and the text inset remain unchanged. No overlay hide/restore lifecycle is introduced.

`validate_issue_239.py` failed on the previous icon ZIndex before the fix and passes with the corrected ordering. This is a source contract, not a rendered screenshot test.

## issue#240: long tips intercept adjacent card selection

`SGC_StarSlash` uses the native combat-pile card selection. `NCardHolder.CreateHoverTips` calls `NHoverTipSet.SetAlignmentForCardHolder` after the text tips have been instantiated. The original hover-tip root and text container ignore the mouse, but `scenes/ui/hover_tip.tscn` contains background and title Controls with default Stop filtering. A long description can cover a neighboring hitbox and intercept mouse input.

The new postfix makes only the text-tip Control subtree mouse-transparent, and only for Shin Getter cards inside `NCombatPileCardSelectScreen`. Other screens and the card-preview container are unchanged. It preserves the description rather than hiding or deleting it. The tip can still temporarily overlap visually; moving onto the adjacent card must now reach that card and replace the old tip. Actual screen behavior remains to be validated.

`validate_issue_240.py` failed with the patch absent and passes with the scoped recursive mouse filtering.

## issue#242: map-scoped free purchase

The previous eligibility check used `LastFreeFloor == TotalFloor`, restoring the free purchase on a later shop floor in the same act map. The existing saved `FreePurchaseActIndices` history already identifies maps where a free purchase occurred.

The fix checks that saved history against `CurrentActIndex`, records only a successful zero-gold purchase, and refreshes the used status after load/property restoration and room entry. `LastFreeFloor` remains serialized for compatibility; the existing array save proxy and clone isolation remain intact. The Chinese, English and Japanese descriptions now explicitly say once per map. Merchant reaction changes from issue#241 are excluded.

`validate_issue_242.py` failed on floor-scoped eligibility before the fix and now passes. The issue#89 assertion was updated from a zero-gold success branch to the equivalent nonzero-gold early return; issue#89 and issue#192 both pass.

## issue#243: not fixed, reproduction still required

The read-only review followed `Hook.ModifyDamage` through `CreatureCmd.Damage`, `SGP_OpenGet.AfterDamageReceived`, and `NShinGetterStaticVisuals.PlayOpenGetVfx`. Original 109 damage still calls the received-damage hook for an avoided zero-damage hit. The three atomic forms reverse and then replay fusion; Dragon uses the existing opacity feedback because it has no fighter frames. The animation-finished handler does not replace fusion with an unrelated action's idle callback. Instant mode skips scaled waits, which may make this feedback unobservable, but the reporter's mode is unknown.

`WillAvoidCurrentHit` is mutable across damage calculations; clearing it during a nested calculation is a candidate, not a confirmed reachable cause. No production changes are made on that basis. Needed evidence: form, source card, accumulated amount, enemy hit amounts/count, speed setting, HP result, power removal, voice result and preferably a recording/log. Existing `validate_issue_10.py` passes, but this does not establish gameplay or animation success for issue#243.

## Re-run commands

Run from `shin-getter-mod-godot`:

```powershell
python -B tools/validate_issue_239.py
python -B tools/validate_issue_240.py
python -B tools/validate_issue_242.py
python -B tools/validate_issue_89.py
python -B tools/validate_issue_192.py
python -B tools/validate_issue_10.py
python -B tools/validate_b130_core.py
python -B tools/validate_b130_feedback.py
python -B tools/validate_b130_ultimates.py
```

All nine gates pass. Core reports 488 source checks; feedback rejects 18 negative variants; ultimates reject 19 negative variants and verify the existing 47 RGBA cells. All 45 tracked JSON files parse, and `git diff --check` passes. These are static/offline contracts only; Harmony runtime binding, input dispatch, save/load in game and rendered visuals are not claimed as tested. Independent review and final combined testing belong to the main development session.
