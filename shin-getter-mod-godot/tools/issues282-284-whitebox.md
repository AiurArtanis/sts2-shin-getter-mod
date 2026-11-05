# issue#282, issue#283, issue#284 white-box regression

## Delivery boundary

Base: `origin/main@26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8` (released formal v1.3.0). Target milestone: v1.4.0. Formal 109 source is read-only. This branch does not include pending feature branches, Beta work, deployment or release changes.

## issue#282: the acquired relic changes the purchase receipt

Original 109 `MerchantRelicEntry.OnTryPurchase` deducts `Cost`, obtains the relic, reads `Cost` again for gold synchronization, and returns the newly evaluated cost as `goldSpent`. `MerchantEntry.Cost` runs all merchant price hooks. `OnTryPurchaseWrapper` clears/restocks the entry before calling `Hook.AfterItemPurchased`, so inspecting the purchased entry's Model in that after-hook is too late.

Before this fix, acquiring Good Citizen enables its own zero-price hook between the debit and receipt: a 230-gold debit can produce a zero-gold receipt, which consumes the current act's free purchase. That is a different cause from issue#242's former floor-scoped reset. The fix excludes a `MerchantRelicEntry` whose current Model is Good Citizen from its own price hook. Its acquisition remains paid, its receipt remains paid, and the next eligible purchase can still be free. Existing per-act persistence and the nonzero-gold early return stay intact. No transaction state is added to save data.

`validate_issue_282.py`: RED on the original source, GREEN after the exclusion. Native purchase, a following purchase, another shop in the act, next-act reset and real save/reload still require combined-candidate runtime validation.

## issue#283: the foreign owner's event does not enter combat

Corrected native entry diagnosis (revision 3): START_FIGHT is `IsProceed=true`. Formal109 `NEventRoom.OptionButtonClicked` runs `option.Chosen()` directly and returns for Proceed; it never ordinarily calls EventSynchronizer.ChooseLocalOption or broadcasts that choice. This local-only entry is why the other peers do not enter the original combat synchronization. The earlier assumption that the actual button necessarily traversed native normal option broadcasting was wrong. The nonlocal Owner early return remains a separate constraint if an option is replicated, but did not prove that this Proceed button had reached that route. Prior commit/review records are retained as historical analysis, not current entry proof.

Round 1 (rejected by independent review): the relay invoked native combat on each peer's local event and used a weak per-event attempt marker. Repeated requests could still add pending owners after combat sync/the first-hand hook. The native sync exchanges player/RNG data, not challenge participants, so opposite peer arrival orders could lose or disagree on the accepted spirit setup. The old source-token gate passed without testing that ordering. Its synchronous reflection catch also could not observe native fire-and-forget transition completion/failure. These are source-level review findings, not recorded multiplayer reproductions; the original P1 report remains at `E:/Work/outputs/issues282-284-review-20261105/review.md`.

`validate_issue_283.py`: RED before the relay, GREEN after it. This is a source-level causal diagnosis, not a recorded multiplayer reproduction. Required runtime cases include Getter host/non-Getter client and the reverse, two/four peers, all-Getter control, single-player control, repeated/concurrent challenges, teammates still in another event selection or already finished, and combat reward/return. Check first-turn spirit setup and checksum/RNG as well as entry completion. No runtime PASS is claimed.

### issue#283 revision 2: acceptance design, rejected entry wiring

The policy below was implemented, but candidate `6387420e` was rejected (P1): IsProceed clicks bypassed its only ChooseOptionForEvent hooks. Getter host Request had no bound _choosingOwner; Getter client Request did not send a request. The 21-case fixture manually supplied that missing route. The report at `E:/Work/outputs/issues282-284-review-20261105/r2/review.md` and its `proceed-route.log` are independent offline evidence of the omission, not game/UI results. The independent product compile also reported 3 nullable warnings. Those facts supersede any reading of the following design description as a working real entry in that candidate.

The host freezes its first valid native START_FIGHT request after native `ChooseOptionForEvent` records option history. This is the single accepted challenger. All other/late requests are unaccepted, rather than accepted challengers whose setup may be discarded. Single-player keeps the original entry path. Non-Getter players never become challengers, and original Trial encounter/rewards/resume policy are unchanged.

`ShinGetterTrialCombatSync` binds to the current EventSynchronizer/run, current mutable Trial and exact RunLocation. Request identity comes from native option routing (sender -> Player -> that Player's event); the choice Prefix binds that Player, and only the matching registered Getter event, enabled invasion setting and START_FIGHT page may propose a request. The choice Postfix commits after native history; it does not suppress or rewrite native choices/history. START_FIGHT is a proceed option, so native NEventRoom.BeforeOptionChosen does not asynchronously delay its callback on this path.

The new public `ShinGetterTrialChallengeMessage` uses native mod INetMessage discovery, reliable buffered delivery and RunLocationTargetedMessageBuffer. It is host-only (`ShouldBroadcast=false`), carrying accepted OwnerId, run seed and location. Clients additionally verify actual NetClient.HostNetId, current run seed/location, active Trial and existing eligible Getter owner. A commit installs that one owner's pending setup before native local-event combat entry; it does not wait for the original START_FIGHT message. Duplicates and late callbacks cannot add owners, resurrect consumed setup, or enter again. Per-peer native encounter instances match their own pending encounter reference.

Handlers attach at EventSynchronizer construction; BeginEvent resets the active event scope. EventRoom.Exit clears pending Trial setup, and EventSynchronizer.Dispose, run CleanUp and disconnect unregister the handler/release scope. Entry is latched as an attempt before invoking native code. The latch is never reopened after broadcast, even on synchronous failure. Native asynchronous failure remains unobserved here; no automatic async recovery is claimed.

The revised source gate was RED against round 1, then GREEN. `tests/Issue283TrialSync` directly compiles the actual controller/message (not the product): 21 CPU checks pass for opposite request arrival, option delayed past commit/sync/first-hand, owner eligibility/authority, old contexts, duplicates, failure and cleanup. Players/transport/packets/native entry/first-hand consumption and history are doubles, and Harmony is not loaded. History consistency in this fixture is modeled only; actual option history/checksum/RNG and in-flight selection/reward cleanup remain NOT_RUN and must stay in the target-candidate runtime queue. No game, PCK or deployment was run.

### issue#283 revision 3: route the actual Proceed button

Only the multiplayer Trial START_FIGHT button is intercepted at `NEventRoom.OptionButtonClicked`. Its existing IsProceed flag stays true. The Prefix passes every unrelated option through untouched. The controller additionally validates the active local Trial, eligible nonnull Owner, current run/location, unlocked/unchosen exact option object and index, live host/client mode and multiplayer count. It then calls native ChooseLocalOption and skips the original local-only Proceed branch. Native sender/Owner routing, choice Prefix/Postfix, broadcast and default history behavior now execute through this actual action; Owner checks were not removed. Singleplayer and other Proceed/normal event paths keep their old route.

BeginEvent, Request and current-event checks now cache/check nullable EventModel.Owner explicitly, without null-forgiving suppression. The CPU double declares Owner nullable and treats warnings as errors. The new button-shaped fixture first failed on the rejected candidate, then passed after the production route was connected. It directly links the production controller/message/patch methods and models native IsProceed dispatch and native event choice/history order. Both Getter-host and Getter-client clicks, reversed two-Getter arrival, repeat/late original options, unchanged unrelated/singleplayer routes and nullable/invalid contexts are covered. All 27 CPU checks pass. The transport, native UI/synchronizer, history and other runtime objects remain doubles; Harmony binding and real UI/history/checksum/reward cleanup are NOT_RUN.

Routing this one multiplayer Proceed action through native normal selection causes its default ShouldSaveChoiceToHistory to be honored consistently on peers; the old local-only Proceed route did not save it. There is no synthetic history write, option deletion or global Proceed policy change. The earlier policy for a unique accepted owner, setup-before-entry and failure latching is retained. issue#282/issue#284 remain unchanged. No product compilation, game, PCK or deployment was performed by this development session.

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

Round 2's nine source gates passed (core510, feedback30, 57 JSON) without detecting the missing Proceed route. Revision 3 adds the exact UI patch binding and route guards to the source gate and expands the directly linked CPU fixture to 27 PASS with warnings-as-errors. All nine source gates pass again (core510, feedback30), all 57 tracked JSON files parse, and diff-check passes. These remain source contracts/controlled doubles only. Product compilation, PCK export/load, Harmony binding, game execution, actual multiplayer and deployment are NOT_RUN in this development session and belong to the main development/acceptance workflow. Independent reviewer builds of earlier candidates are not product or gameplay results for this revision.
