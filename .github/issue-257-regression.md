# issue#257: v1.3.0 two-card timing regression

Scope: Expansion Strike and Shining Spark only, formal109.
Base: `22a544c6df32baa4aa4953e7f58918d59f61fadf`.

## Changes

- Expansion Strike owns its custom voice cue. Wait up to 0.86s only when a
  positive-duration voice actually starts; execute the attack at rush contact,
  then recover position/scale. Missing visual nodes still invoke the attack.
- Shining Spark returns from its rush at contact. The existing 0.08s impact
  stage runs concurrently with the initial attack; Ki followups, recovery and
  sequence cleanup remain in order.
- Existing B130 source gates reflect the new timing contract. No artwork,
  numeric balance, recording driver or other card changes are included.

## Focused Regression (2026-10-16)

- formal109 `dotnet build --no-restore`: 0 warnings, 0 errors.
- `tests/Issue257CardTiming`: 9/9 production-source-linked logic cases passed.
- `tests/Issue257CardTiming/test_contracts.py`: 3/3 supporting source gates passed.
- `git diff --check`: passed.

The executable uses simulated game command, voice and VFX dependencies. This
round did not launch a game or OBS and does not establish native GPU, audio,
multiplayer or cold-start performance acceptance. No full-run or Beta regression
was performed. Only this scoped CPU round was requested and performed.

## Separate Historical Native Evidence

The unchanged product timing patch was used for R3 `expansion01` and
`spark02-warm` recordings. The expansion tight edit was accepted by the user.
The spark warm trace recorded 31.3935ms from frame44 to native damage entry;
the cold native starry-impact initialization cost of 1126.3961ms remains
unresolved. Warm footage is not evidence that cold performance was fixed.

Local evidence directory:
`E:/Work/StS2 Mods/_issue-evidence/promo-timing-integration-20261016`.
Historical native evidence:
`E:/Work/GitHub/OpenMontage/projects/shin-getter-promo-r3-20261015/incoming/dev`.

Issue remains open for tracking. Integration does not itself deploy or release.
