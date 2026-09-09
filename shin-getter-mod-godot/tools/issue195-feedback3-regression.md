# issue#195 feedback3: fake merchant portrait size

## Scope and evidence

- Target: formal 109 only, based on origin/main at
  `96c6de0e907480f213000ec5d2b9f71a592cbeba`.
- Artanis added a size failure under Obsidian BUG ticket test feedback2.
  Reference images in the all-in-one vault: `Pasted image 20260909231740.png`
  (ordinary shop) and `Pasted image 20260909231840.png` (fake merchant).
- The previous compensation removed the event container's extra scale, but
  matching the shop's absolute portrait size did not match the larger event
  composition. Apply a bounded 10 percent increase to the fake merchant only.
- Keep the inverse container scale. Apply the new multiplier to both portrait
  scale and center offset, preserving the same local origin. Bounds continue
  to derive from the resulting sprite rectangle for multiplayer placement.
- Effective scale becomes 0.4136 and vertical center offset becomes -212.9336
  before outer viewport transforms. Foot placement remains near the previous
  position, not an independently measured in-game pixel guarantee.
- Ordinary shop scenes, citizen portrait, Architect, voice and BGM code are
  unchanged. Keep full fallback-body hiding to prevent the ERROR texture.
- The BGM report remains unresolved. This change does not claim to fix it or
  change the default music rule.

## Regression

1. Extend `validate_issue_195.py` before the production edit: RED (exit 1),
   missing multiplier, portrait scale and position guards (four failures).
2. After the production edit: GREEN (exit 0). Validate the event-only 1.1
   constant, offset/scale symmetry, Bounds, fallback hiding and original shop
   values. Arithmetic checks cover container scales 1.0, 1.75 and 2.0.
3. `python shin-getter-mod-godot/tools/validate_issue_21_31.py`: PASS.
4. `python shin-getter-mod-godot/tools/validate_issue_88.py`: PASS.
5. Parse all 41 tracked project JSON files: PASS.
6. `git diff --check`: PASS.

Static assertions are not gameplay validation. No compilation, PCK export,
game instance, Beta change, shared deployment or automation restart this round.
The main development task owns independent review and subsequent integration.

## Gameplay checks remaining

- Judge the enlarged portrait against the fake merchant scene at the usual
  resolution, without requiring equal absolute size to the ordinary shop.
- Check head/feet clipping and foot placement.
- Check the fallback ERROR texture remains hidden.
- Check multiplayer spacing against other players.
- Check ordinary shop/citizen portrait and Architect idle remain unchanged.
