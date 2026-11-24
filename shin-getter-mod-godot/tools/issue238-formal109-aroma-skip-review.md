# issue#238 formal109 Aroma skip localization

## Boundary and evidence

Local base: `integration/v1.4.0-20261118@1c60ed8d2c7fe17db8aab8712e0b1954a69f5975`. The main developer explicitly confirmed this local formal109 candidate as the base, not the older GitHub main. Delivery branch: `fix/issue-238-aroma-skip-109-20261119`. Local commit only: no push of unpublished ancestors, PR, merge, deployment or Beta work.

The independent natural-run report at `E:/Work/outputs/v140-agent-desktop-20261119/formal109/TEST-MATRIX.md` marks the Simplified Chinese Aroma/Hayato candidate page as FAIL. `natural02/ui/059-hayato-options.png` and `061-hayato-reentry.png` both show English Skip; ordinary card rewards in that run show the localized skip text. Cancellation/reentry and one 6-HP confirmation were observed separately as working on the old candidate. Those observations and the original FAIL are not modified by this source fix.

Formal109 `NChoiceSelectionSkipButton._Ready` only overrides the scene's English Skip if its optional name was set. The native `CardSelectCmd.FromChooseACardScreen(..., canSkip:true)` path does not provide such a name. Native `card_reward_ui/OPTION_SKIP.name` already supplies zhs `跳过`, eng `Skip` and jpn `スキップ`.

## Minimal delta

The existing fourth-event interaction checkpoint exposes a read-only predicate for an active/current selection whose model is AROMA_OF_CHAOS and whose exact option is the Hayato initial route. The existing native choose-a-card presentation adapter uses it to update only that screen's `SkipButton/Label` via native LocString/SetTextAutoSize. It then calls the same `BeforeSelectionResult(screen.CardsSelected())` as before.

There is no new Harmony target or global native-button/resource replacement. Unowned selectors, another event/route, stale/disposed contexts and the grid adapter do not receive this text update. The event handler, target/candidate cache, RNG, costs, commit, history/discovery and save operations are unchanged. No translated files or PCK resources change; text is resolved from the current native locale at each presentation/reentry.

## Developer checks

- New source contract: RED before the predicate/UI assignment, GREEN after; six negative mutations rejected.
- Nine CPU checks link the actual checkpoint source with identity doubles and pass with warnings-as-errors. They test scope/control flow, not a rendered UI or native payment.
- Actual read-only native catalog values for zhs/eng/jpn match the reused key; no rendered-glyph PASS is claimed.
- Existing source checks: save-boundary/33 negatives, Brain ownership/12 negatives and item-choice font/6 negatives PASS.
- All 70 tracked JSON files parse; diff-check PASS.
- No full mod build, native engine, PCK export/load, game test, public push or shared deployment is performed by this development session. Source review and runtime tests remain pending.

## Other formal diagnostics: attribution only

- Frame warning is from loading the mod's `card_frame_shin_getter_mat.tres`: Godot cannot resolve the shader UID in its runtime map and explicitly falls back to `res://shaders/shin_getter_hsv.gdshader`. The material UID matches the source shader `.uid` file, and that text path exists. This identifies the resource lookup path, not a proved broken frame or the cause of a visual overlay. The official selected-card state is not covered by the cited atlas image. No material/UID edit is made to merely suppress the warning.
- Native font subresource duplication and D3D12 PSO diagnostics remain raw observations. The resource path/engine diagnostic alone does not establish a ShinGetter-owned fault.
- Natural02's normal exit0 still logs 2018 CanvasItem RIDs, ObjectDB instances and 326 resources in use. These aggregate shutdown records lack a resource-owner allocation/free trace or a controlled formal baseline that would attribute them to this delta. They are not waived, and exit0 is not resource-clean acceptance.
- The confirmed 113 Beta missing-frame issue is deferred by Artanis and is not a formal109 reproduction. No Beta code, resources or tests are touched.

No additional formal product defect is established by this read-only attribution. If later evidence identifies one, it needs its own finding and scoped correction, not a speculative change bundled with this text fix.
