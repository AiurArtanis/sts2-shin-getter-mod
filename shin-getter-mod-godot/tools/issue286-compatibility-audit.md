# issue#286 - v1.4.0 / 111 Beta Regression Candidate

This is the issue#93 child for the user-authorized community compatibility
regression. It is an isolated test integration, not a release or target-line merge.

## Integration Boundary

Formal candidate f6266fa8 is the authority for reviewed gameplay and resources.
The 111 target base is 3b1d2079. Conflicted gameplay/resources select the reviewed
formal content; 111 APIs and the narrow existing BS2 modifier adapter are retained
or reapplied. No source game, main checkout, shared deployment or user save changes.

## Native111 Adaptations

- Retain CardPlay propagation, creature damage, power overrides, animator,
  potion-factory and lobby adaptations from the existing Beta line.
- Pass the actual owner to new Annotations/event item choice contexts.
- Retain canonical encounter input/identity semantics, including the new
  committed multiplayer Trial entry.
- Use SetUpSavedSingleplayer and preserve NetGameType and forceInTest in the
  exact scoped IncrementNumReloads wrapper call; ordinary calls remain native.
- Native111 RNG uses four state words, not the 109 counter-only replay. Store
  all four plus counter in production checkpoint JSON and restore through
  native LoadFromSerializable. Missing/short/zero state and invalid counters
  fail explicitly. No guess migration of a counter-only 109 checkpoint.
- Do not add the 109-only SavedPropertiesTypeCache patch: that type does not
  exist in 111. Native111 discovers the actual saved properties directly.

## Initial Failure and Narrow Fix

Candidate 07583525 compiled and passed old issue#93 target checks, but the
new issue#283 constructor was missing from that older probe. Its first actual
startup failed on the still-109 five-argument EventSynchronizer attribute.
The failed attempt and raw evidence are retained, not reclassified as passing.

The 111 constructor adds IRunState and uses a 64-bit seed. The corrected
attribute specifies all six arguments; callback, protocol field, writer and
reader retain ulong without truncation. The linked-production real-Harmony
probe now uses the pinned native111 DLL and confirms 7 targets/9 hooks, rejecting
missing and wrong constructor signatures. The source game was only read.

## Evidence Limits

- Native111 compilation: zero warnings and errors.
- Production DTO JSON round trip plus 32 actual native RNG draws passed;
  four malformed state payloads rejected; scoped reload call prefix verified.
- Existing issue#93 metadata/reflection probe passed.
- Real Harmony target resolution passed, without installing patches or running
  game callbacks. This does not establish actual initialization or gameplay.
- PCK independently loads 207 textures, 94 audio, 167 atlases, 52 JSON and
  1008 files. Against A, only res://ShinGetterMod.json differs by content hash.
- BaseLib also failed during the first startup. Native runtime provenance is
  still under investigation; do not blame it on official111 without proof.
- Hex exact package/source is missing. Startup-only no-Hex probes do not count
  toward the complete community group's victory/75-room acceptance.

## Original Native Reference Recovered

The 6896 input is a locally decompiled/recompiled Debug binary. Original111
ExportRelease DLL 0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9
was recovered from the old Beta worktree reference: 9757184 bytes, MVID
73b63ee0-6c0a-47bb-b0d1-b21f6d94222e, product commit41cef1ea. Its PE CodeView
path is the publisher's ExportRelease build path. Historical native handshake
and October19/20 v1.3.0 runtime identity records independently bind the same
hash/MVID to v0.111.0; these records do not grant this candidate acceptance.

Rebuilding against this original reference passes with zero warnings/errors;
the 7-target actual-Harmony probe and production checkpoint/native RNG probe
also pass against it. The target gate now pins this exact original reference.
The metadata tool accepts a separate dependency directory but never falls back
to a different sts2.dll. New runtime initialization still requires its own result.

The currently installed BaseLib3.4.7 and St2bs2.3.0 DLL hashes match those
October runtime records. Their failures with the old reconstructed runtime are
not evidence that these packages fail on the recovered original game DLL.
