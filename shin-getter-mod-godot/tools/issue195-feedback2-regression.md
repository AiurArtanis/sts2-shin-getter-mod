# issue#195 Feedback 2 Regression

## Scope

Formal 109 only. No game instance, production deployment, or Beta change is part
of this branch validation.

## White-Box Reproduction

- `NFakeMerchant.AfterRoomIsLoaded` creates combat visuals, invokes
  `StartCharacterAnimation`, and uses `visuals.Bounds.Size.X` for player spacing.
- `scenes/events/custom/fake_merchant.tscn` scales CharacterContainer by 1.75;
  the ordinary merchant container has unit scale. Copying the merchant sprite's
  0.376 scale and -193.576 vertical offset without compensation multiplies both.
- `shin_getter.tscn` inherits `fallback.tscn`. Its body is a Sprite2D with
  `res://images/monsters/error.png`; SelfModulate alpha zero hides that texture
  without hiding child form sprites. The old event patch restored alpha to one.
- `EventModel.EnterCombatWithoutExitingEvent` assigns ParentEventId for both
  fresh combat scenes and transitions from visual-only event layouts, regardless
  of whether the event resumes after combat. The old opening voice logic only
  checked the current map point for Unknown, missing other event entry paths.

## Static RED / GREEN

Run from the worktree root:

```powershell
python shin-getter-mod-godot/tools/validate_issue_195.py
```

The extended validator fails on the old product source for the opaque fallback,
uncompensated sprite placement/scale and missing ParentEventId voice guard.
It passes after the fix. Its optional argument is the Godot project directory.
The script also locks the existing event BGM category/configuration path.
This is source-level and layout-arithmetic coverage, not rendered-pixel or audio
playback acceptance.

## Runtime Acceptance Handoff

- Enter Fake Merchant through the real event and verify the portrait is the
  ordinary shop size at the same viewport, without ERROR text or form sprites.
- Check multiplayer portraits: per-player spacing uses the resized portrait
  bounds; other characters and the parent event container are unchanged.
- Check Architect visual-only idle and ordinary shop/combat visuals unchanged.
- Trigger actual event combat from Fake Merchant, Battleworn Dummy, Dense
  Vegetation, Mysterious Knight, Punch-Off and Architect. TunnelerNormal should
  be tested through The Lantern Key event, not treated as event-only by its ID.
- Verify opening cue 029 for event-origin combat even when the previous map
  point is not Unknown. Visual-only narrative screens must not start the cue.
- Select a concrete EventCombat BGM *before* triggering combat. Verify that track
  plays for the real event transition, and ends/restores normally afterward.
- Keep the existing Default/Random/Silent/local-player rules. The archived
  issue#88 design explicitly defines event Default as retaining original music.

## BGM Evidence Boundary

The current user configuration read on 2026-09-09 has EventCombatBgmTrackId set
to `default`. The gameplay log shows the Onslaught preview after Fake Merchant
combat had already started. That does not establish which track was configured
at combat entry. The existing production music routing already checks
ParentEventId, not Unknown map points; no unproven music replacement or new
default track is introduced. Concrete-track failure still needs the exact
configuration-at-entry and playback evidence, or clarification of new default
behavior. Do not sign this reported audio symptom as dynamically fixed.
