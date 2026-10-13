# issue#255 — lossless Star Slash metadata and draw-work reduction

Baseline: `02d97982c90d9741f7a98ec5ee16fdf9bb0c2bc9`, on the existing
`fix/formal109-render-cold-20261013` worktree. Formal109 sts2.dll SHA-256:
`C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`.
The earlier bounded render-preparation P1 fix/history is retained. This work does not
touch issue#254, Beta, mother images/models, game sources, shared deployment or saves.

## Reversible runtime schema

| Form | Legacy JSON bytes | Compact formatting alone | Runtime schema2 bytes | Polygon uses → stored unique | Point uses → stored |
|---|---:|---:|---:|---:|---:|
| Getter One | 2107619 | 497673 | 445490 | 4909 → 4269 | 31384 → 27355 |
| Shin Getter Dragon | 4146361 | 941038 | 742719 | 10471 → 7937 | 62556 → 46517 |

Schema2 has one exact-coordinate polygon pool per form, with ordered references in
all five roles: weapon, blade, handle, armor foreground, fingers. The real production
reader parses each pool contour once and shares arrays. `BladeCover` and `HandleCover`
remain distinct runtime geometry inputs. No coordinate rounding, hull, tolerance-based
simplification, role deletion, image resize or frame removal is used.

Runtime frames retain duration/grip/axis and all five polygon-role lists. Editing-only
index/source frame/source PTS/stage/PNG SHA fields reconstruct exactly from the unchanged
ART-003 delivery.json, checked by a canonical delivery digest. Top-level ART-004 provenance
hashes are retained. Expanding both generated JSONs equals the entire legacy documents,
including editing fields, polygon order and every number; generating again is byte-exact.
147 frame mappings and weighted 2.4s envelope, hold/impact/raise/cleave/recovery, voices,
selection and damage effects remain unchanged. The actual reader supports legacy JSON
and schema2; unknown versions and invalid references are rejected.

Historical ART-004 review/diagnostic artifacts are not rewritten. The importer validates
their runtime source hashes against the pinned historical fingerprints actually recorded
in the existing approved diagnostic (637e750a / b7fcb90d),
instead of falsely identifying new code as the old reviewed geometry. Current source
equivalence against the pre-optimization production `02d97982` is separately tied to
the source hashes in the managed checks artifact. These are different evidence layers.
The importer, repair generator and existing validators use the same reversible exporter.
The six native-topology repair evidence/hashes remain historical and unchanged.
The actual retained ART-004 approved inputs were replayed through `import_star_slash_frames.py data`;
both outputs matched the standalone schema generator's exact bytes/hashes. Source annotations,
review files and diagnostic files were read only.

## Animation and calculation reduction

No adjacent PNG hashes or complete pose/coverage frames are identical in either form.
Removing frames would require a visual/timing tradeoff and is not justified here. The
actual production cleaner can remove only four source vertices in Getter One and zero
in Dragon; armor/finger layers have zero redundant vertices. These are already handled
by the existing cleaner. Exporting fewer points provides negligible additional draw
savings and would change raw union inputs, so coordinates/points are preserved. Lossy
contour simplification is deferred rather than trading hand/blade quality for a gate.

The useful reduction is in repeated computation:

- Per-frame axis, blade root/width and handle-tail projections are computed once at
  metadata construction; `BuildWeapon` no longer flattens/scans those contours per pose.
- The fixed four-cubic reference profile's 18-sample float32 evaluation is performed
  once. Each pose still maps those exact 72 samples to its current growth/recovery.
  No final axe geometry is cached by PNG frame alone; `poseTime` remains continuous.
- Source weapon meshes cache Local → CleanPolygon → native triangulation, exactly as
  before. Foreground meshes separately cache CleanPolygon(source) → triangulation →
  Local. Their UVs still use cleaned source pixels and actual atlas origin/size.
- Source arrays and native union operand order remain identical; native MergeOutline
  and its predicates are unchanged. There is no pre-union or hull approximation.
- Only the last exact (frame, poseTime, transform) weapon draw is retained. Repeated
  hold-pose redraws reuse core/light/outline/centerline arrays. New poseTime recomputes
  dynamic geometry and union; H/V/center/offset changes invalidate local caches.
- Polygon caches belong to one sequence, are bounded by its form's unique contours,
  and clear on End along with draw arrays. They trade temporary retained memory for
  repeated triangle work: measured vector/index payloads were 663484 / 1111988 bytes,
  excluding object headers/dictionaries/textures. This is not process RSS or VRAM.

| Path | Before | After |
|---|---|---|
| First form JSON access | Read full legacy string, parse repeated polygon arrays once per static Loaded key | Smaller runtime string, parse unique pool once; still one static Loaded key |
| New dynamic pose | Rebuild normalized cubics, flatten blade/handle, triangulate all cover pieces, native union | Map precomputed cubics, use scalar projections, triangulate source pieces only on cache miss; dynamic meshes/union remain |
| Same exact pose redraw | Rebuild/triangulate/union plus temporary LINQ/list arrays | Submit cached arrays; no BuildWeapon/triangulation/union |
| New foreground frame | Clean/triangulate every armor/finger polygon and assemble vertices/UVs/indices | Reuse per-contour triangles; frame vertices/UVs/combined indices still assembled |

## Developer verification (not independent review or native GPU acceptance)

- Formal109 production build: 0 warnings / 0 errors.
- `star_slash_runtime_metadata.py` reproducibility/exact inverse: PASS. 735 independently
  rasterized legacy/decoded 720px masks (all roles, including fingers): XOR 0;
  decoded contour error 0 because coordinates/order are exactly equal.
- `Issue255StarSlashMetadata`: 109966 assertions PASS, directly linked current Data
  and Sequence plus exact Git baseline sources with only class-name aliases. Actual
  installed GodotSharp vector structs verify 1323 continuous geometry samples exactly;
  within-frame geometry changes in 42 / 47 frames confirm growth/recovery is continuous.
- 9408 draw instrumentation cases compare actual submitted vertices, fake indices,
  colors, order and atlas UVs across frames/poses/centering/offset/H/V/HV. Repeated exact
  poses invoke zero triangle/union work; transform invalidation and End cleanup PASS.
  Fake native union keeps contours separate; fake triangulation returns fans. These
  are cache/input/ordering proofs, not native concave triangulation/rendering evidence.
- Instrumented triangle calls: Getter One 108224 → 80464; Dragon 207232 → 138352.
  Instrumented union calls halve because each pose is deliberately redrawn twice;
  do not report this as a real GPU speedup. Stub logging dominates reported draw
  allocation totals, which are preserved in the raw artifact with that scope.
- Actual production constructor rejects 22 malformed schema/data inputs. Python schema
  gate rejects 16 data/provenance/order mutants, including changed coordinates, weights,
  frame deletion, missing hands and invalid references. Existing source/timing/geometry
  gates: 9 models + 46 source/cleaner mutants, issue124 full 63 mutants PASS.
- Existing built-production Issue124VoiceMask: 4096 voice-bit pair checks, actual delivered
  schema2 constructor, both 76/71 frame maps, concave blades/light/taper and 18 invalid
  variants per document PASS; source-independent phase/voice/card code remains unchanged.

Managed-only measurements from the recorded run:

| Kernel | Getter One old → current | Dragon old → current |
|---|---:|---:|
| JSON parse + production constructor median ms (7 samples) | 14.4420 → 11.1593 | 30.2059 → 20.2586 |
| Constructor allocated bytes in that sample | 1228368 → 1260576 | 2541248 → 2203600 |
| BuildWeapon allocation, ten poses per frame | 15236680 → 11229800 | 14704680 → 10491000 |
| BuildWeapon elapsed ms, same batch | 18.4284 → 12.5665 | 19.2954 → 11.6244 |

Constructor measurements exclude engine GetFileAsString/I/O, and JSON buffer-pool/JIT/GC
state can vary. Getter One constructor allocation increases slightly; no blanket load
allocation reduction is claimed. BuildWeapon and draw-stub measurements are managed
CPU/allocation observations, not first-load GPU/texture latency, FPS or player acceptance.

## Reproduction and handoff

From the worktree root:

```powershell
python -B shin-getter-mod-godot/tools/star_slash_runtime_metadata.py --apply
python -B shin-getter-mod-godot/tools/star_slash_runtime_metadata.py
python -B shin-getter-mod-godot/tools/repair_star_slash_foreground.py
python -B shin-getter-mod-godot/tools/validate_issue_255_star_slash.py
python -B shin-getter-mod-godot/tools/validate_b130_star_feedback.py
python -B shin-getter-mod-godot/tools/validate_issue_124.py
dotnet build shin-getter-mod-godot/ShinGetterMod.csproj --nologo -v:minimal
python -B tests/Issue255StarSlashMetadata/prepare.py
dotnet run --project tests/Issue255StarSlashMetadata/Issue255StarSlashMetadata.csproj --nologo
```

Metadata audit: `tools/issue255-star-slash-metadata.json` (generated/reproducible).
Managed raw checks/source hashes:
`E:/Work/StS2 Mods/_issue-evidence/combat-stutter-20261012/issue255-star-slash-managed-checks.json`.
Test-only old sources/JSON are regenerated from Git into excluded fixture obj inputs.
Original editable art and approval artifacts are preserved.

The main/test chats own packaging, independent review and isolated actual GPU comparison
of full product versus no JSON read/no outside-axe overlay. No diagnostic bypass is
added to production. Any controller bypass must record which phase/selection/visual
work it skips; the full animation remains the product. This production chat has not
started an engine/game/graphics process or changed shared deployment. Native comparison
results are not yet available here. Prior v19/v20 measurements are not this revision's
benchmark; cold texture work, ~60ms sprite loads and device-loss cause remain separate.
issue#255 returns to pending review for this new work; 13 manual tasks stay unchecked.
No PR/merge/PCK export/Tag/Release/Issue closure is performed.
