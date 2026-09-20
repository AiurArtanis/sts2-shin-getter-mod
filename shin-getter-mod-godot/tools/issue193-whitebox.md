# issue#193 global BGM switch

2026-09-20 delivery: source review only. Build, validators, game/Godot, PCK,
initialization, deployment and UI screenshots have NOT been run.

## Contract

- `BgmEnabled` in the existing `shin_getter_chunibyo.json` defaults to true,
  preserving the behavior of older configs without this property.
- The native tickbox precedes the existing expandable BGM header. Off disables
  both mouse activation and focus on the header, collapses details and returns
  a displaced focus to the tickbox. It does not disable the tickbox itself.
- All five categories, random and explicit tracks, other-character replacement,
  execution themes and preview start/resume are gated before track resolution.
- Off immediately stops all owned music players, kills their fades and releases
  pending execution fade waiters. Native music volume is restored through the
  existing restore functions. SFX, voice, stored game volume and tracks are untouched.
- Re-enabling permits the next normal trigger or a manual preview. It does not
  immediately restart an encounter track or replay an already consumed finisher.
  Off does not consume a finisher trigger. This policy is stated in all three locales.
- Save failure restores the old bool and tickbox/header state, shows the existing
  localized error popup, and leaves playback untouched. Success broadcasts only
  after persistence and stop. No per-category selection is cleared.
- UI config subscriptions use EnterTree/ExitTree; repeated Ready does not rebuild
  controls. The new fields are instance-owned, never static Node references.

## Official 109 source contracts inspected

- `NTickbox.IsTicked` updates visuals without emitting Toggled; only OnRelease
  emits it. Restoring a failed save therefore does not recursively save.
- `settings_tickbox.tscn` is 320px wide with hard-coded reticle offsets. The
  leading 72px slot resets those offsets to FullRect and keeps original visuals.
- `NDropdown.OnVisibilityChange` closes an open floating list when hidden;
  CloseDropdown also attempts focus restoration, so the master header refresh
  schedules focus on the still-visible tickbox afterward.
- `NSubmenu` stores `_lastFocusedControl`; hiding the BGM section redirects a
  cached descendant focus as well as current viewport focus.

## Deferred checks (not evidence of passing)

Run `validate_issue_193.py`, regress issue#88, issue#7 and issue#153, parse tracked
JSON and perform the official 109 build only after the test hold is lifted.
In an isolated game profile exercise all five categories; preview playing/paused;
execution fade-in/fade-out; other characters; master off on load; save failure;
old-config default; key/mouse/controller navigation; expanded dropdown collapse;
three locales; close/reopen; and off/on preserving selections and voice/SFX.
