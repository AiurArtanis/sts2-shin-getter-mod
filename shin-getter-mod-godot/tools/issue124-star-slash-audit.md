# issue#124 / issue#215 Star Slash development audit

Date: 2026-10-05. Status: development complete; independent code review and runtime acceptance pending.

## Scope and authority

- Isolated branch `feat/issue-124-star-slash-20261004`, approved base `e95c760111040297ff7950ce29b750bbbde5cbf2` on the B1.3.0 line derived from formal v1.2.2. The root checkout and the balance worktree are not development targets.
- Artanis requested Getter One and Shin Getter Dragon's complete raised-axe / selection hold / confirmation / downstroke sequence, plus a Getter-line colored weapon that covers the baked axe and extends beyond its 720px canvas. Existing `PlayHeavyCleave` and `vfx/vfx_giant_horizontal_slash` stay unchanged.
- Read-only voice authority: `D:/Library/Pictures/杀戮尖塔2-素材/sound/音频字幕.xlsx`, sheet `完整设计`, A67/B67/D67/E67 for 066, A68/B68/D68/E68 for 067. 066 is Dragon's `燃烧吧！真盖塔龙！`; 067 is One's `上吧！真盖塔！`. E68 currently contains the full filename instead of a subtitle; the display text is normalized from B68, not a claimed spreadsheet correction or human confirmation. The workbook is unchanged.
- These two preparation cues are the entire issue#124 scope. issue#215 remains a larger B1.3.0 item with historical pending acceptance; this animation delivery must not mark unrelated work accepted.

## Selection and audio contracts

- Inspected formal109 `CardSelectCmd.FromCombatPile` and `CardSelectorPrefs`: zero candidates and fixed-count `count <= MinSelect` auto-selection do not open the native UI. The new preparation path mirrors this boundary, excludes custom/noninteractive/replay selectors, and never forces manual confirmation.
- Raising advances concurrently with the native selector. Early confirmation still completes the real raise once. Holding chooses the explicit held source frame rather than the next cleave frame.
- 066/067 route by actual powers: Dragon first, then One, not Getter1 compatibility. Confirmation keeps existing 016 / Getter1-and-Dragon compatibility, default run-once, Always, and Silent behavior.
- Selection return stops only this Owner's preparation audio/subtitle, then starts 016. Exhaust commands and all hooks remain awaited. Remaining 016 time accounts for elapsed exhaust-hook time, with a finite extra hold cap.
- Existing gameplay is retained: 3 cost, 22 base damage, 1->2 exhaust selections, printed-value stack cap50, one Getter1-compatible HotBlood grant, one main attack and the original impact VFX. Other forms or non-rendered/Instant execution retain the named legacy fallback.

## Save compatibility and lifecycle

- Released cue indices0..30 keep Low bits0..30; indices31..61 keep High bits0..30. New62 uses Low's previously unused sign bit and63 High's sign bit. Production Add/Contains share a checked0..63 mapping; negative saved ints are intentional, not invalid values. No relic save-field type or serialization schema was changed.
- The per-play sprite-child sequence owns a metadata token. Pause freezes its visual clock; death, form/animation changes, ancestor hiding, combat ending, Instant transition and exit release pending tasks and local visuals. Cleanup may restore idle only while it still owns the same live action; it cannot replace death or a newer action.
- Paused Star Slash is protected from generic Attack/HeavyAttack/Cast/Dash/Hit and its own Block/Idle reactions. Suppressed actions consume queued speed multipliers. Dead/Death remain high-priority interrupts.
- Source PNGs remain byte-exact ART-003 imports:76 One frames and71 Dragon frames,720RGBA. Two10-column7200x5760 lossless sheets use the original2.4s variable frame weights rather than count/30. No re-keying, RGB/alpha edits, video edits, source-workbook writes or baked impact replacement occurred in this worktree.
- Exact hold sources are One133 and Dragon130, at0.90s/1.05s respectively. The main impact event stays1.40s; weighted FrameAt then displays One151/index44 and Dragon138/index39. ART-004's150/137 impact-candidate labels are source-reference markers, not a claim those frames are frozen at the event. No frame-weight retiming or backward pose jump was introduced to force those nearby candidates.

## Verification performed so far

- Baseline RED: `python tools/validate_issue_124.py --expect-red e95c760111040297ff7950ce29b750bbbde5cbf2` rejects the old missing preparation/native-selection contract.
- Source and delivered-data GREEN: the full gate passes147 mapped frames and rejects46 deliberately broken source/data variants (26 source +10 per form). Both exact ART-003 PNG provenance and native fixed-selection eligibility are checked.
- Managed C# harness executes production VoiceHistoryMasks:4096 pairwise checks, old mapping, signed-bit integer values and Add/Contains range checks pass. UV checks cover8 sheet regions and standalone texture fallback. Production synthetic76/71 and actual76/71 metadata, weighted FrameAt, every coverage/body/hand polygon, reference-driven concave Bezier/taper geometry at frame start/midpoint/end/hold and18 invalid metadata variants per fixture pass. It does not run Godot, a game scene, native polygon Union or native SavedProperties roundtrip.
- Formal109 reference build succeeds with0 warnings/0 errors; reference DLL SHA-256 `C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`.
- Foreground texture sampling explicitly unwraps the SpriteFrames AtlasTexture, then maps source pixels through its Region origin and the full sheet size. Production managed UV checks cover8 sheet regions and standalone texture fallback. Sheet size and white modulation are obtained once per redraw, not per foreground vertex. This proves coordinate arithmetic, not native GPU pixels.
- Final validation sweep:38 applicable existing scripts plus the new issue#124 gate pass (39 total). `validate_issue_181.py` is an old v1.2.1 publication gate, already incompatible with the v1.2.2 baseline; its version pin is not removed or bypassed. issue#214 combined193 passes. Builder checks35 sheets and4 SpriteFrames. Final prospective inventory parses50 JSONs with0 failures and checks579 declared Godot UIDs with0 duplicates (including .uid/.import and .tres/.tscn resource headers). Diff-check passes. Explicit staging must retain the same50 JSON set before commit.
- Final formal109 build:0 warnings/0 errors. Production DLL SHA-256 `784637CC904B894B8829125D81C97BC5DA0DB825EEF802285F54C584E3F2AF7A`. This is the isolated development build, not a final integrated/deployed build.

## Visual review boundary

ART-004 control root: `D:/Library/Pictures/杀戮尖塔2-素材/anim-sprite/workspace/issue215-star-slash-20261004`.
Actual147-frame metadata and overlays are now frozen and strictly imported after offline review. The earlier full-contact-sheet reviews found shaft drift, old-handle foreground leakage and leg-armor occlusion; those calibration defects required corrections rather than accepted limitations. Artanis also rejected the placeholder head/rectangular shaft and supplied `Scene-403-01.jpg` as a shape reference. Production uses the pipeline's two asymmetric cubic-Bezier lobes and a smoothstep tapered shaft; Godot4.5.1 `Geometry2D.MergePolygons` is used only for union outlines, not a convex hull. Exact proposal data is retained at `tools/star-slash-reference-geometry.json`. Offline import approval does not claim final user-art or native GPU pixel approval.

On 2026-10-05 initial complete offline reviews checked76 One and71 Dragon frames against metadata SHA c81036ac /4fa308df. Dragon source127/index30 and189/index62 had separated green shafts (approximately25px/37px axis displacement); One source177/index61 had a provisional old-dark-axe/second-axis finding. The candidate was revision-requested and could not pass the import gate. Those three findings were returned to the designated pipeline for targeted source-based correction, not broad glow coverage. Previously corrected Dragon27/34/40 shafts and34/40/138/183 metal-head leaks were separately confirmed closed; original gray/purple energy fans and shadows are not globally removed.

Final calibration freezes One metadata `236373FB93C93BA845552D81257FFBF96271D269E05DCC50B4EA0DE255027CCC` and Dragon `7740C25E8B3CCBE9F1014DA961E0CBC884FC1B4D709FEB33C388496073D0260C`. Original full reviews plus targeted corrections close the two Dragon shafts. Two independent One177 RGBA reviews retract the broad black-axe/second-axis classification: source (460,550)/(600,562) alpha1/3 are background; (615,575)/(572,647) are original light/fan. The actual old metal ring lip (489–491,521), alpha255, is locally covered with opaque (37,219,103). The defect was missing cover, not foreground texture reintroduction. No broad fill or glow conceals it.

Direct frame-by-frame JSON comparisons with retained old SHA backups confirm only One177 and Dragon127/189 changed:75 One and69 Dragon records are identical (144/147 total). All30 final result outputs match SHA; metadata/diagnostics/current C# source and reference profile match. `review.json` approves only this offline metadata/import, not user art or game acceptance. Strict import generated both animation.json with exact annotation/review provenance. Original147 PNGs and the reported2106 protected inputs remain unchanged. Both final GIFs total2400ms. Payload scans found no inline image payloads.

Audio066/067 are byte-exact copies of B67/B68 source WAVs, SHA-256 respectively `7C0B38ADC354B32ED2CF46F65D765B351F5FD8434DEDCD2533A188D6A2DF19BB` and `A3FD52B8601F5267FFF256FE93366CE10BF7F7123D1BBEB2A7D416B6CFF6F8ED`. No transcoding or source audio modification.

Pillow diagnostics are not Godot pixel/render, battle-scale occlusion, audio synchronization or input acceptance evidence. The actual runtime and diagnostic geometry must match before final import. Uncertain source gray fans / dark purple details remain distinguished from replacement-weapon errors.

The deeper-notch revision2 proposal failed the production managed gate: lobe tip segments33/36 self-intersected, and the first light band paired equal Bezier indices rather than equal axial distances. The first inner control point was corrected from(0.88,0.25) to(0.88,0.12), preserving the deeper remaining inner arc. Light now pairs the sampled inner/outer arcs at equal axial coordinates and uses the28%/72% inside ribbon. Synthetic fixtures pass shape self-intersection and point containment without weakening assertions. A chained PowerShell command's final successful Python exit briefly masked the middle managed failure; this was explicitly corrected to the pipeline, and following verification commands check each exit before continuing. Revision2 is not a passed result.

The later actual-metadata audit found73 One and100 Dragon body-foreground contours with proper segment self-intersections. Offline raster filling is not proof that Godot can triangulate these contours. Both delivered-data Python and production-data managed harness now check all cover/body/hand polygons; malformed source contours must be split/repaired by the pipeline before final import. These173 candidate defects are not waived as visual tolerances. The harness also checks production weapon geometry at each frame's start, midpoint, end and held time.

The pipeline split only unsafe foreground contours into simple row-merged rectangles:127 One contours became3826 parts (4250 total body parts, maximum188/frame);180 Dragon contours became8777 parts (8892 total, maximum260/frame). Offline raster XOR is0 and polygon self-intersection checks pass. This is not native triangulation, GPU sampling or frame-time performance evidence; those remain runtime acceptance boundaries.

## Explicitly not performed

No PR, merge, tag, release, PCK export/validation, game/Godot startup, isolated initialization, gameplay automation or deployment. Formal109 sources, root checkout, user local states, existing balance worktree and shared Steam/daily-Godot installation directories are unchanged. The release manifest remains v1.2.2. Both issues remain open; main-developer independent review is required after final delivery.
