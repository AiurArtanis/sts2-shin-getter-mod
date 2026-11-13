# issue#287: game v0.113.0 adaptation in progress

Parent: issue#93. Effective 2026-11-13, 113-beta is the only maintained Beta target. The existing 111 branches and evidence remain historical and must not receive new 111 development, validation, or releases.

## Identity and Starting Point

- Mod source base: `1ad7daf1f5828b09ed97a5b6c17e3118df0216d3`, the tested v1.4.0 candidate on the previous 111 integration line, not a v1.4.0 release.
- Official new game: `v0.113.0`, branch `v0.113.0`, commit `16ac0fe3`.
- Official `sts2.dll` SHA-256: `A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7`.
- New source/manifest candidate: `v1.4.0-beta.113`, minimum game `0.113.0`; not published.
- Decompiled source and CodeGraph are analysis aids. The actual official DLL is the compile reference, not the reconstructed Debug assembly.

## First Compile Adaptation

The unmodified candidate failed on the removed `MerchantEntry.OnTryPurchaseWrapper`. This first pass then builds against the official 113 reference with zero warnings and zero errors. It is not a complete dynamic compatibility or runtime verdict.

| Area | Change | Current evidence |
| --- | --- | --- |
| Merchant presentation | Clear the presentation marker at the actual `OnTryPurchaseUnsynchronized(MerchantInventory, bool)` implementation, with the full Harmony signature. Do not call that unsynchronized method from mod code or replace the native purchase synchronizer. | New native entry/source inspected; compile passes. Actual patch binding and successful/failed purchase lifetimes remain pending. |
| Event relics, four sites | Pass each actual `eventModel.Rng` to `PullNextRelicFromFront`; do not consume the card/combat reward RNG. | Explicit event RNG overload/source inspected; compile passes. Deterministic native reward checks remain pending. |
| Maximum HP, four sites | Replace the old non-card boolean with `cardSource: null` for the same event costs. | New native `CardModel?` signature/source inspected; compile passes. Native behavior checks remain pending. |
| Star Slash selection | Inspect the owning player's `CardSelectorStack.GetSelector(Owner)` and local-only selector when deciding whether selection presentation is needed. Continue using native `FromCombatPile`. | Per-player stack and native command source inspected; compile passes. Actual manual/automatic selection timing remains pending. |
| Annotations selection | Read the owner's deterministic selector rather than removed static `Selector`; preserve native local-only selector choice synchronization separately from remote waiting and the existing cancelable UI. Never install a global selector. | Stack/source inspected; compile passes. Confirm/cancel, owner isolation and multiplayer checks remain pending. |

## Remaining Work Before Delivery

- Complete the full mod direct-member, override, Harmony/AccessTools and private-reflection comparison against the official 113 assembly. Compilation does not verify dynamic targets or transpiler layouts.
- Inspect merchant presentation transpilers and Good Citizen Permit consumption semantics across the native synchronized purchase pipeline. Blitz entries must not accidentally inherit standard-shop assumptions.
- Review event checkpoint/Continue hooks, RNG serialization and private fields against 113; do not transfer the old 111 audit verdict.
- Determine applicable existing behavior in Blitz/Dread and character recognition for the new character without silently removing features or inventing unapproved game design.
- Add focused checks and independent review; separately record actual native initialization and targeted behavioral results when authorized. Do not claim full gameplay, multiplayer, resource, audio, visual or performance acceptance from this build.
- Refresh active Beta validation/download documentation before release. Historical 111 artifacts and audit files remain historical; new 113 checks must not mislabel them as 113 results.

No shared deployment, target-line merge, Tag, Release or new gameplay regression was performed by this first compile pass. issue#287 remains OPEN / in development.

## Second-Pass Source and Mode Audit

Additional incompatibilities found beyond the original eleven compile sites:

- Native welcome audio/dialogue moved out of `NMerchantInventory.Open` to the async `ShowOpenInventory.MoveNext`. A separate narrowly targeted transpiler loads the actual captured inventory field, while retaining the original timing, branch structure and Blitz dialogue path. The purchase transpiler remains on the non-async `OnPurchaseCompleted` method.
- Native `FromSimpleGrid` now creates an `NSimpleCardSelectScreen`, but official IL still calls the inherited `NCardGridSelectionScreen.CardsSelected` declaration. An initial derived-type MethodInfo change in this work failed the actual IL probe because its ReflectedType differed. The binding/adapter has been restored to the declaring base method; that baseline binding was not an original 113 incompatibility. Failed probe evidence is retained. The result Task wrapper itself is unchanged.
- Blitz purchases can complete for zero gold through the same native merchant pipeline. The certificate must not consume its ordinary-shop allowance or replace Blitz greeting/feedback for these native free choices. Guards apply specifically to Blitz, not to Standard or Dread shops; the synchronizer is not replaced.
- Custom runs are now represented by a mode plus `IsCustomGame`. Empty-modifier custom Standard runs must not write solo bond progress, supply eligible prior-run history, or use the `sgd` progress-writing operations. Read-only status and main-menu editing retain their existing rules.

The independent mode/save review is in `issue287-modes-and-save-audit.md`. No unresolved, confirmed mode/save P1 or P2 remained after these source changes. Standard non-custom solo bond rules are retained; Dread ordinary events, Mandala, shops and Continue are not disabled. Default Blitz maps have no ordinary event nodes, so those paths are naturally unreachable rather than failed patches. New character/rotating selection and exact native save behavior still require the focused runtime cases; source/metadata conclusions are not gameplay acceptance.

Source/reference gates: `python -B tools/validate_issue_287.py` (16 checks). These include the pinned official DLL, source/manifest contracts, per-owner selection, both event-selection adapters, custom guards, Blitz boundaries and native event reward ownership. They are not native gameplay tests.

## Native Event Save Verification Finding

The first BrainLeech fourth-option native attempt reached the real option but failed
the complete-run save readback gate at event-history `variables`. Its exact comparison
snapshots were not captured; the original failure is retained, not reconstructed.
The original written file contains an empty variable object. A later diagnostic-only
repeat with the unchanged old candidate captured the exact same Comparison: expected
has `variables:{}`, direct native readback omits it, and that is the sole difference.
The first attempt's missing snapshots are not retroactively filled in.

An official-DLL CLR reproduction confirms a native representation instability:
the history condition sees a nonempty dictionary, but the native converter filters
unsupported values and non-string derived DynamicVars to `{}`. After reading that
object, the empty dictionary is omitted on the next serialization. The same converter
and history condition exist in 111; this is not proven to be a new 113 regression.

The scoped fix only canonicalizes an empty JSON object to omission in comparison-only
StableJson at `map_point_history/[act]/[point]/player_stats/[player]/event_choices/[choice]/variables`.
Original NativeJson, nonempty variables, wrong types, unrelated names/paths, clocks,
map-false preservation and transaction/write/readback boundaries retain their rules.
No DTO normalization roundtrip, save-file change or global serializer patch is added.
The new `tests/Issue287EventSaveVariables` is RED on the original candidate and GREEN
after this fix with positive/negative official-serialization and JSON contract checks.
That is not a native event, Continue, rollback or full compatibility pass.

## Native Event Reward Ownership Finding

The save-verification fix passed three actual native comparisons, and the same H04
case then reached the real maximum-HP cost and card selection. The reward failed
because its preview card already had an owner, while `RunState.AddCard` requires an
unowned card and binds the owner itself. The transaction restored the economy; this
does not prove every reflected rollback field. The 111 setter also rejects this
double assignment, so the finding is not attributed to a new 113 setter change.

BrainLeech now creates candidates with the standard native `RunState.CreateCard`
path before upgrading and selecting. The selected card is already registered and
is passed directly to `CardPileCmd.Add`. Its returned success and actual `cardAdded`
owner/deck membership are checked, not the original candidate's membership: native
hooks such as FresnelLens can legitimately replace the input with an enchanted clone.
The canonical cache, native selection, costs, transaction, RNG and other routes are
unchanged. Source review and compilation do not replace the pending native replay,
FresnelLens replacement case or rollback verification.

An independently packed candidate PCK was loaded in an empty Godot 4.5.1 project: 1008 files, 207 textures, 94 audio streams, 167 binary atlases and 52 JSON files, `failed=false`. No game was launched for this asset verification. Assets retain the previous approved runtime payload; no new artwork or sound was made or approved here.

Full direct-member/override/Harmony/AccessTools/IL probe results and actual initialization are recorded separately when available. Do not interpret this section as clearance to release or shared-deploy the candidate.
