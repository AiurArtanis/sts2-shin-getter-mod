# issue#238: formal109 empty event-history variables

The first formal109 Brain Leech attempt wrote its native save, then stopped
before presentation/payment/reward because the direct native readback omitted
an empty `variables` object. The archived Comparison001 has exactly this
difference; both original titles and all 51 map flags are equal. The old attempt
remains RED and its byte arrays/manifest are unchanged.

The product delta is byte-identical to the verifier reviewed in `bf5d5bee`
(blob `9356d59e`). Only comparison stabilization changes. Capture retains the
original `NativeJson` array, the direct map flags and all existing Saved/error
boundaries. There is no DTO deserialize/serialize normalization and no change
to native persistence, serializer, Brain Owner handling or Annotations cancel.

In the stabilized comparison view, omit `variables` only when it is an empty
object at `map_point_history[][] . player_stats[] . event_choices[] . variables`.
Container types must match at every level. Empty/missing is symmetric after
this normalization. Null, arrays, scalar values, nonempty objects, similarly
named paths and all adjacent/gameplay fields remain strict. Existing exclusions
remain exactly the two root clocks. Original numbers retain their JSON lexemes.

Verification (offline only):

- Entered-room source contracts and 33 unsafe mutations pass; the two additional
  mutations reject discarding nonempty variables and using the wrong path.
- The new source-linked pure CLR executable compiles only the verifier and
  unchanged map-scope helper against exact official109 dependencies. It does
  not build the mod/game project or install patches. Both helper builds finish
  with zero warnings/errors; an initial missing test-only namespace import is
  preserved in the first build logs and corrected in the final builds.
- The unchanged baseline helper confirms the real archived RED at exactly the
  reported variables path (18 checks, expected failure observed).
- The repaired helper passes 75 contracts, including the actual preserved pair,
  official109's map-free native converter fixture, diagnostic original-byte
  retention, every archived false-map true/missing mutation, strict nonempty/
  null/type/path/adjacent/gameplay checks, numeric lexemes and duplicate keys.
- All four original diagnostic byte arrays, manifest and results hashes are
  pinned; tests read them without rewriting them. Root worktree, its five dirty
  tracked files, the old integrated candidate and four protected user files
  remain unchanged.

These checks establish only source/CLR comparison behavior. A new native attempt
must repeat the original Brain construction and verify actual SaveFourth,
candidate presentation, subsequent cost/reward and cleanup. Owner was not reached
in the original RED and is not declared fixed here. Do not promote the original
attempt or an offline comparison into native acceptance.
