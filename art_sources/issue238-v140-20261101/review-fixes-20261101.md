# issue#238 review corrections (2026-11-01)

The technical review of `86894555` rejected three mechanisms. This continuation addresses those findings in the same isolated branch. Artanis approval of the nine images and two rewritten event designs remains pending. No release, deployment, or new art is included.

1. **Annotations manual cancellation.** Two call-site adapters in the original `PlayCardAction.ExecuteAction` state machine prepare this card's first selection before `SpendResources`, then skip `OnPlayWrapper` entirely for a canceled attempt. The native action, spending method and play wrapper remain in control. Other cards return their original spending task and call their original wrapper. A `GameActionPlayerChoiceContext` participates in the native queue; hand, target and `CanPlay` are checked again after selection resumes. The adapter calls native cancellation for UI cleanup, resets per-attempt state, and propagates exceptions. It does not suppress global card hooks or replace the state machine body.

   Once the native play is committed, a canceled automatic/replayed selection skips the remaining enchantment/Vigor effects while retaining costs, previous effects, native records/hooks and result piles. There is no late energy refund or return to hand. The three-language committed-selection prompt states that distinction. A target invalidated by pre-play hooks is selected again under these committed rules. Enchantment exceptions restore the target's prior enchantment and log the failure; they do not create a refundable play after native hooks have started.

2. **Ember plus Tablet.** After either upgrade direction, `ChangeUpgradeLevels` reapplies the tracked combat-only removal of Exhaust. Native `DowngradeInternal` may reset canonical keywords, but that no longer cancels Ember's duration. Permanent deck versions remain outside the affected set; end-of-combat restoration retains the existing boundary.

3. **Filter plus in-hand transformation.** `AfterCardEnteredCombat` records new Status instances already placed in hand. At `AfterCardChangedPiles`, that marker permits a new replacement's `Hand -> Hand` callback while ignoring ordinary reordering of the same hand instance. The marker is removed on the callback, is keyed by relic instance, and is cleared before combat. `_used` is still set before nested Exhaust hooks. Existing consumption/evolution context and hook ordering remain unchanged.

Feature-session checks: updated issue#238 structural/source contracts PASS, 4 changed C# files parse with zero syntax errors, and the formal 109 assembly's actual IL has one captured action field, one spending call and one play-wrapper call with matching signatures and ordering. The static IL receipt is `E:/Work/outputs/issue238-v140-20261101/review-native-il-receipt.json`. This is not mod compilation, patch execution, gameplay or UI acceptance.

Main-review narrow execution still required:

| Case | Expected result |
|---|---|
| Letter Opener, at least two legal targets; repeatedly cancel the first manual selection | Energy/stars, pile, enchantments, Vigor, skill-play count and enemy HP unchanged; no native play-wrapper entry. |
| Cancel, then submit a normal successful play | Selection opens normally; cost, enchantment, bonus and native result pile settle once. |
| First play succeeds; a later native replay selection is canceled | Pay once; previous effects remain; no new enchantment/Vigor from canceled effects; native replay records/hooks and final pile remain valid. |
| Ember removes Exhaust from upgraded Annotations; Tablet downgrades, upgrades and downgrades it | Exhaust stays absent for that combat; successful use follows the resulting native non-Exhaust pile; permanent card remains unchanged. |
| Zero-cost Drill Missile returned to hand by native Feral Power, then replaced by Broken Drill | New Status is filtered once, Exhaust hooks execute and Evolution is granted; ordinary same-instance hand reordering does not consume Filter. |
| Nested Exhaust callback and new combat | No duplicate trigger; before-combat reset restores availability and clears instance markers. |

Run only these related cases, with ShinGetterMod and any explicitly disclosed test/input driver. The prior 20-event/gameplay matrix and human art review are not rerun or promoted by these checks. The candidate manifest still uses main's version; distinct protocol/version isolation remains a gate before any external distribution or mixed-version multiplayer.
