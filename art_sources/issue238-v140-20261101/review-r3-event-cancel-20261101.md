# issue#238 R3: event card-selection cancellation

Continues `239d1dc276327c9f5119ca3763f3bc3f4004e4fd` after main's explicit R2 closeout/unfreeze. This change covers only the six permanent-Deck selection rules and Aroma's no-Pile candidate screen. No Annotations/C/D code, art, authored localization, journal schema/RNG, costs or rewards are changed. Brain Leech's deliberately noncancelable choice remains unchanged. Old 1a9/R2 sandbox inputs, products and raw evidence remain frozen.

## Native API and UI

Read-only PE metadata of the actual formal109 `lib/sts2.dll`, SHA-256 `C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`, confirms both methods are public/static with the expected parameters. Native source bodies were inspected, not inferred from the UI flag:

- `CardSelectCmd.FromDeckGeneric(Player, CardSelectorPrefs, filter, sortingOrder)` filters real current Deck instances, uses `NDeckCardSelectScreen` and native mutable-deck-card choice serialization. The six routes now call it with the existing legal rule, route prompt, Cancelable and RequireManualConfirmation. One legal card therefore still requires explicit confirmation and can Back out; zero legal cards return before opening a screen. Returned cards are revalidated against actual Deck membership, Owner, Pile and the rule.
- `CardSelectCmd.FromChooseACardScreen(context, cards, player, canSkip)` supports up to three supplied candidates and native index synchronization. Aroma supplies only its existing saved-ID mutable candidates with Owner assigned. `NChooseACardSelectionScreen` renders with PileType.None and has a real Skip button with MegaInput.cancel; it never needs a permanent Deck Pile. It uses the original game's generic choose-card banner. The event's existing route description supplies the transformation/cost context; this native banner and 1/2/3-candidate layout remain runtime/visual review items.

No selection hook is installed. Native selectors supplied by the game retain their existing command behavior; interactive acceptance must use Selector=null. No private native property or global selection patch is introduced.

## Event lifetime and commit boundaries

A narrow await wrapper captures the actual public native run reference and current EventRoom, checks them before and after selection, and verifies Owner/run and model/room identity plus cleaning/finished state. Native overlay task cancellation while still in that same event returns an empty choice, so the existing RunFourth path restores initial options. A run/room exit becomes a scoped OperationCanceledException caught only to release Busy; it does not reopen a stale event or show a transaction popup on the main menu. This does not alter native GameAction CompletionTask or certify all native shutdown tasks.

All selection remains before CommitFourth. Aroma still saves its target identity, then fixes and saves the candidate IDs using the existing no-replacement RNG path before opening the candidate choice. A canceled candidate leaves those saved IDs intact for re-entry/restore. Only a confirmed valid candidate enters the existing transaction to pay 6HP and transform. Save helpers, target identity, candidate restoration, CommitFourth and rollback bodies are unchanged. Other non-cancellation exceptions retain the existing visible error path.

## Validation and remaining execution

Feature-session validation is static only: issue#238 source/persistence/localization gates, new cancellation/commit-boundary source contracts, changed C# Roslyn syntax, Python syntax and diff checks. No DLL build, game launch, PCK export or old sandbox mutation is performed here. Formal109 compilation and runtime are main's responsibility.

Suggested incremental evidence after source/build review:

1. Six legal-rule routes: native Back, preview Back then whole-choice Back, single target cancel/confirm, zero target; no cost/reward/completion on cancel, original options remain usable. Share actual UI-path evidence where appropriate while checking each route's filter and transaction separately; do not call this a 20-route pass.
2. Aroma: cancel target step; Skip candidate step with 1/2/3 candidates as feasible; re-enter and save/load retain target/candidate IDs, then one confirmed 6HP/transform and completion. No-Pile preview must have no selection/Pile ERROR.
3. Same-event overlay removal and actual run/room exit: observe task/Busy/overlay cleanup, absence of stale initial-page update/popup and no late commit. Keep native task and process-exit receipts distinct from behavior; do not infer missing receipts.
4. Brain Leech remains noncancelable, and original event options still advance normally. Do not rerun passed Annotations/C/D matrices.

Main's R2 report (`E:/Work/outputs/issue238-review-20261101/review-239d1dc2-runtime.md`) independently passed A/B cancellation, normal cleanup, 24 formatter combinations and minimal Drill behavior. Its real-exit result is limited to execution-task/queue/context/listener cleanup: native CompletionTask remains pending. A's two missing exit receipts, Godot/stderr resource ERRORs and the unassigned old ShaderMaterial P1 remain unresolved. Those limits, nine art approvals and two redesigned-story approvals are not cleared by R3. No merge, shared deployment or release is performed.
