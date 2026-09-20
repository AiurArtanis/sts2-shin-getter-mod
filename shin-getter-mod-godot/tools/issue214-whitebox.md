# issue#214 catalog refresh / limited issue#193 integration test scope

## Source and decisions

- Snapshot: sheet `BGM列表`, `A2:E25`; the sheet named `原设计` is history,
  not the current selection list. Exact row/title/asset SHA-256 mapping is in
  `issue214-bgm-manifest.json` (23 concrete tracks, default first, random last).
- Retain stable IDs for retained tracks; `heroic` now has the authoritative
  English title `Bravery`. Do not migrate it to Iron Saga's separate recording.
- Sheet A25 says `Bravery(機動戰隊Ver).mp3`; the supplied directory has a single
  matching recording named `Bravery(機動戰隊).mp3`. This explicit filename alias
  is recorded in the manifest; source assets and workbook were not renamed.
- Added creation, hostility, forward, its_time, bravery_iron_saga. Removed
  grief, morning_on_the_tundra, brutality, memory, interference,
  cold_bloodedness, resolve, heats_final from selection/resources/random pool.
- Default event combat (ParentEventId) uses Onslaught in all acts. Hive elite
  now uses Forward. Default execution uses OVA Bravery (heroic), not Iron Saga.
  Other explicit elite/boss defaults remain unchanged. Unspecified normal
  defaults retain Getter Robo / STORM / DRAGON / HEATS pending user direction.
- Startup normalizes all five saved IDs (unknown, deleted, null -> default;
  supported IDs including random survive). No startup disk write: next normal
  successful config save persists normalization; read-only config still works.
- Ancient exclusion, local-player scope, relative volume, execution trigger
  timing, random algorithm, and preview behavior are unchanged.
- issue#193 is a separate reviewed change. Do not recreate its switch here.
  Combine the two branches in an isolated review checkout for the joint tests.

## Review / test boundary

This delivery has NOT run compilation, validators, Godot, game, PCK, initialization
or deployment. Artanis authorizes the main task to run automation **after review**,
only for issue#214 and issue#193, preferably headless. No full validate glob,
full-run, unrelated card/event/multiplayer regressions, release or deployment.

1. Build the isolated combined checkout with official 109 references (prerequisite).
2. Run `python tools/validate_issue_214.py --combined193`. This invokes only the
   issue#214 and issue#193 source contracts. Legacy issue#57/#88 catalog fixtures
   were updated for future consistency, but their broader suites are out of scope.
3. Headless targeted runtime: inspect all 23 actual AudioStream loads, selected
   resource paths, default event/Hive elite/execution mappings; explicit selection
   overriding default; random resolves only to retained concrete tracks. Do not
   claim source text or SHA checks prove decoder/playback behavior.
4. Isolated config profile: deleted/unknown/null IDs in each of five fields fall
   back to default on Load, valid/random IDs remain; successful later save and
   reload retain repairs. Confirm voice settings and master false remain unchanged.
5. Master off: encounter/execution/preview start, paused preview resume and loop
   remain suppressed; closing active playback restores original BGM volume and
   completes pending fade waiters. Re-enable does not replay consumed execution.
6. Limited UI fixture: native tickbox disables/collapses the BGM row; floating
   dropdown closes; focus stays navigable; save failure rolls back state and
   does not stop playback. Repeated open/close does not duplicate subscriptions.
7. Scope checks use a local/remote character fixture, not a full network run:
   remote Shin Getter alone does not change local music; opt-in other-character
   setting and Ancient exclusion remain respected. No new multiplayer harness.

Human-only residuals: audible quality/volume, real keyboard/controller feel and
three-language layout. A headless stream state is not evidence that these pass.
Report exact unsupported runtime assertions instead of broadening test scope.
