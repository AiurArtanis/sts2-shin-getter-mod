# issue#282, issue#283, issue#284 white-box regression

## Delivery boundary

Base: `origin/main@26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8` (released formal v1.3.0). Target milestone: v1.4.0. Formal 109 source is read-only. This branch does not include pending feature branches, Beta work, deployment or release changes.

## issue#282: the acquired relic changes the purchase receipt

Original 109 `MerchantRelicEntry.OnTryPurchase` deducts `Cost`, obtains the relic, reads `Cost` again for gold synchronization, and returns the newly evaluated cost as `goldSpent`. `MerchantEntry.Cost` runs all merchant price hooks. `OnTryPurchaseWrapper` clears/restocks the entry before calling `Hook.AfterItemPurchased`, so inspecting the purchased entry's Model in that after-hook is too late.

Before this fix, acquiring Good Citizen enables its own zero-price hook between the debit and receipt: a 230-gold debit can produce a zero-gold receipt, which consumes the current act's free purchase. That is a different cause from issue#242's former floor-scoped reset. The fix excludes a `MerchantRelicEntry` whose current Model is Good Citizen from its own price hook. Its acquisition remains paid, its receipt remains paid, and the next eligible purchase can still be free. Existing per-act persistence and the nonzero-gold early return stay intact. No transaction state is added to save data.

`validate_issue_282.py`: RED on the original source, GREEN after the exclusion. Native purchase, a following purchase, another shop in the act, next-act reset and real save/reload still require combined-candidate runtime validation.

## issue#283: the foreign owner's event does not enter combat

Original 109 `EventSynchronizer.HandleEventOptionChosenMessage` executes an individual event's choice on every peer for the selecting player. Native `EventModel.EnterCombatWithoutExitingEvent` then raises its notification but returns when that event's Owner is not local. The prior temporary IsShared override only satisfies its first guard. Thus only the selecting Getter's peer reaches `RunManager.EnterRoomWithoutExitingCurrentRoom`; that method starts combat synchronization and awaits peers that did not take the same path. A non-Getter teammate cannot independently choose the Getter-only option.

For multiplayer Trial only, the fix invokes the same native combat method on `EventSynchronizer.GetLocalEvent()` on each peer, preserving the selecting player as the owner of the pending spirit-card setup. A weak, per-local-event entry marker prevents multiple native entries and makes repeat requests reuse the encounter reference used by the first transition. It retains the temporary IsShared scope and removes that scope in finally; synchronous failures also roll back the marker. It does not turn Trial into a shared-vote event, expose the exclusive option to non-Getter characters, replace network messages, alter rewards, or change single-player/Byrdonis entry.

`validate_issue_283.py`: RED before the relay, GREEN after it. This is a source-level causal diagnosis, not a recorded multiplayer reproduction. Required runtime cases include Getter host/non-Getter client and the reverse, two/four peers, all-Getter control, single-player control, repeated/concurrent challenges, teammates still in another event selection or already finished, and combat reward/return. Check first-turn spirit setup and checksum/RNG as well as entry completion. No runtime PASS is claimed.

## issue#284: missing native card capability metadata

Original 109 `CardModel.GainsBlock` defaults to false. Native `Nimble.CanEnchant` requires both the original enchantment restrictions and GainsBlock; a BlockVar or Defend tag alone does not satisfy it. Fresnel Lens uses that same predicate for rewards, merchant creation and cards being added to the deck. Basic `SGC_Defend`, among other direct block skills, omitted the override.

The patch adds only `GainsBlock => true` to these ten active direct block skills: SGC_BackupPlan, SGC_BlackArmor, SGC_DarkCape, SGC_Defend, SGC_Guts, SGC_HedgehogTactic, SGC_Jammer, SGC_ShedLoad, SGC_Specialization and SGC_TacticalRetreat. SGC_PressureBreath and SGC_SeizeFuture already declare it and stay unchanged. Their designs and direct GainBlock calls were checked. No card numbers, costs, upgrades or effects change.

There is deliberately no base-class inference from BlockVar, no Fresnel/Nimble patch and no retroactive enchantment. Indirect power/plating/transform gains, damage-to-block and block theft are not automatically opted in by this patch; those different mechanics are not inferred from the vague report. Legacy unregistered card classes are untouched. Native existing-enchantment, card-type and ownership restrictions remain in force. Exact reported card/source is still useful for confirming coverage.

`validate_issue_284.py`: RED on the ten missing overrides, GREEN for twelve explicit positive skills and negative power/nonblock/base-class boundaries. Real reward/add-to-deck, enchanted block amounts, display and save/reload are not executed here.

## Static checks

Run from `shin-getter-mod-godot`:

```powershell
python -B tools/validate_issue_282.py
python -B tools/validate_issue_283.py
python -B tools/validate_issue_284.py
python -B tools/validate_issue_58.py
python -B tools/validate_issue_89.py
python -B tools/validate_issue_192.py
python -B tools/validate_issue_242.py
python -B tools/validate_b130_core.py
python -B tools/validate_b130_feedback.py
```

All nine source gates pass. Core reports 507 source checks; feedback rejects 30 negative variants. All 57 tracked JSON files parse; `git diff --check` passes. These are source contracts only. Product compilation, PCK export/load, Harmony binding, game execution, actual multiplayer and deployment are NOT_RUN and belong to the main development/acceptance workflow.
