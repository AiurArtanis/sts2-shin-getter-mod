# issue#282, issue#283, issue#284 white-box regression

## Delivery boundary

Base: `origin/main@26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8` (released formal v1.3.0). Target milestone: v1.4.0. Formal 109 source is read-only. This branch does not include pending feature branches, Beta work, deployment or release changes.

## issue#282: the acquired relic changes the purchase receipt

Original 109 `MerchantRelicEntry.OnTryPurchase` deducts `Cost`, obtains the relic, reads `Cost` again for gold synchronization, and returns the newly evaluated cost as `goldSpent`. `MerchantEntry.Cost` runs all merchant price hooks. `OnTryPurchaseWrapper` clears/restocks the entry before calling `Hook.AfterItemPurchased`, so inspecting the purchased entry's Model in that after-hook is too late.

Before this fix, acquiring Good Citizen enables its own zero-price hook between the debit and receipt: a 230-gold debit can produce a zero-gold receipt, which consumes the current act's free purchase. That is a different cause from issue#242's former floor-scoped reset. The fix excludes a `MerchantRelicEntry` whose current Model is Good Citizen from its own price hook. Its acquisition remains paid, its receipt remains paid, and the next eligible purchase can still be free. Existing per-act persistence and the nonzero-gold early return stay intact. No transaction state is added to save data.

`validate_issue_282.py`: RED on the original source, GREEN after the exclusion. Native purchase, a following purchase, another shop in the act, next-act reset and real save/reload still require combined-candidate runtime validation.

## issue#283: the foreign owner's event does not enter combat

Original 109 `EventSynchronizer.HandleEventOptionChosenMessage` executes an individual event's choice on every peer for the selecting player. Native `EventModel.EnterCombatWithoutExitingEvent` then raises its notification but returns when that event's Owner is not local. The prior temporary IsShared override only satisfies its first guard. Thus only the selecting Getter's peer reaches `RunManager.EnterRoomWithoutExitingCurrentRoom`; that method starts combat synchronization and awaits peers that did not take the same path. A non-Getter teammate cannot independently choose the Getter-only option.

Round 1 (rejected by independent review): the relay invoked native combat on each peer's local event and used a weak per-event attempt marker. Repeated requests could still add pending owners after combat sync/the first-hand hook. The native sync exchanges player/RNG data, not challenge participants, so opposite peer arrival orders could lose or disagree on the accepted spirit setup. The old source-token gate passed without testing that ordering. Its synchronous reflection catch also could not observe native fire-and-forget transition completion/failure. These are source-level review findings, not recorded multiplayer reproductions; the original P1 report remains at `E:/Work/outputs/issues282-284-review-20261105/review.md`.

`validate_issue_283.py`: RED before the relay, GREEN after it. This is a source-level causal diagnosis, not a recorded multiplayer reproduction. Required runtime cases include Getter host/non-Getter client and the reverse, two/four peers, all-Getter control, single-player control, repeated/concurrent challenges, teammates still in another event selection or already finished, and combat reward/return. Check first-turn spirit setup and checksum/RNG as well as entry completion. No runtime PASS is claimed.

### issue#283 revision 2: one host-committed acceptance boundary

The host freezes its first valid native START_FIGHT request after native `ChooseOptionForEvent` records option history. This is the single accepted challenger. All other/late requests are unaccepted, rather than accepted challengers whose setup may be discarded. Single-player keeps the original entry path. Non-Getter players never become challengers, and original Trial encounter/rewards/resume policy are unchanged.

`ShinGetterTrialCombatSync` binds to the current EventSynchronizer/run, current mutable Trial and exact RunLocation. Request identity comes from native option routing (sender -> Player -> that Player's event); the choice Prefix binds that Player, and only the matching registered Getter event, enabled invasion setting and START_FIGHT page may propose a request. The choice Postfix commits after native history; it does not suppress or rewrite native choices/history. START_FIGHT is a proceed option, so native NEventRoom.BeforeOptionChosen does not asynchronously delay its callback on this path.

The new public `ShinGetterTrialChallengeMessage` uses native mod INetMessage discovery, reliable buffered delivery and RunLocationTargetedMessageBuffer. It is host-only (`ShouldBroadcast=false`), carrying accepted OwnerId, run seed and location. Clients additionally verify actual NetClient.HostNetId, current run seed/location, active Trial and existing eligible Getter owner. A commit installs that one owner's pending setup before native local-event combat entry; it does not wait for the original START_FIGHT message. Duplicates and late callbacks cannot add owners, resurrect consumed setup, or enter again. Per-peer native encounter instances match their own pending encounter reference.

Handlers attach at EventSynchronizer construction; BeginEvent resets the active event scope. EventRoom.Exit clears pending Trial setup, and EventSynchronizer.Dispose, run CleanUp and disconnect unregister the handler/release scope. Entry is latched as an attempt before invoking native code. The latch is never reopened after broadcast, even on synchronous failure. Native asynchronous failure remains unobserved here; no automatic async recovery is claimed.

The revised source gate was RED against round 1, then GREEN. `tests/Issue283TrialSync` directly compiles the actual controller/message (not the product): 21 CPU checks pass for opposite request arrival, option delayed past commit/sync/first-hand, owner eligibility/authority, old contexts, duplicates, failure and cleanup. Players/transport/packets/native entry/first-hand consumption and history are doubles, and Harmony is not loaded. History consistency in this fixture is modeled only; actual option history/checksum/RNG and in-flight selection/reward cleanup remain NOT_RUN and must stay in the target-candidate runtime queue. No game, PCK or deployment was run.

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

All nine source gates pass after revision 2. Core reports 510 source checks (round 1: 507); feedback rejects 30 negative variants. All 57 tracked JSON files parse; `git diff --check` passes. The separate CPU fixture reports 21 PASS. These are source contracts/controlled doubles only. Product compilation, PCK export/load, Harmony binding, game execution, actual multiplayer and deployment are NOT_RUN in this development session and belong to the main development/acceptance workflow. Independent review separately compiled round 1 with 0 warnings/errors; that is not a build or gameplay result for this revision.
