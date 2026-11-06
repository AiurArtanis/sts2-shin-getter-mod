# B1.4.0 Logic Delivery

Date: 2026-11-06

Branch: `ticket/balance-b1.4.0-270-271-285-20261106`

Base: `origin/main@26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8`

## Approved Scope

- issue#271: Chosen One remains a 2-cost power. Each copy contributes 1 Ki and
  3 Block per transformation, or 5 Block when upgraded. Existing stacking and
  transformation dispatch are unchanged. The historical balance gate now pins
  the approved 3/5 values instead of 4/6.
- issue#285: Saint Dragon Roar gains native `Eternal` alongside `Exhaust`.
  Damage is 20/28 plus 5/8 per exhausted Getter card. Draw, hand and discard
  scope, voice timing, stun and damage ordering are unchanged. `Eternal` is
  the native non-removable-from-deck keyword, not `Retain`.
- issue#285: newly granted Mandala Saint Dragon Roar has no automatic Corrupted
  enchantment. Other event enchantments, other card sources and existing saved
  enchantments are unchanged. Event reward descriptions are updated in all
  three languages. Existing card descriptions use dynamic values already.
- issue#270, logic only: rename the relic to Musashi's Hat in three languages,
  retaining `SGR_MusashiClone`, IDs, resource names and the saved
  `TriggeredThisCombat` bool. Once per combat, prevent the complete final lethal
  HP-loss amount instead of healing 30% after reaching zero HP. Getter 1 gains
  1 Valor/Hot Blood, Getter 2 gains 1 Accel, Getter 3 gains 1 Persist/Indomitable.
  Shin Dragon gains all three in that order, using the form at trigger time.

## Fatal Damage Boundary

The native `Hook.ModifyHpLost` Postfix runs only for phases including
`AfterOsty`. It sees the result after normal reductions and powers, then
reserves the saved combat charge synchronously and returns zero. It appends
the relic to a copied modifier list without replacing existing listeners.
The native After callback clears its pending form snapshot before its first
await, flashes the relic and grants the rewards sequentially. The original
zeroed HP-loss amount is not run through newly applied powers again.

The patch uses priority `Priority.Last - 2`: `-1` is Harmony's unspecified
priority sentinel, not a lower-than-Last priority. The existing Fighting Spirit
cancellation uses `Last`. Owner, active-hook, melted, alive, combat and used
guards prevent direct lookup from bypassing the usual listener eligibility.
This does not promise ordering against arbitrary third-party patches.

Block already spent on the hit is not restored. Direct kills that bypass
HP-loss processing are not intercepted. New nested damage and subsequent hits
use the normal newly granted powers; global Indomitable behavior is not changed.
The existing saved charge is not renamed or migrated. A loaded `true` charge
remains used and receives no retroactive reward. Combat start/end clear transient
pending state and reset the charge as before.

## Audio Boundary

No new voice, subtitle line, audio path, cue, playback mask, voice service or
voice file is included. Artanis explicitly separated voice production and
manual approval into chat `01a159be-6ee4-76a1-9d76-b0af8b7bd9e6`. That chat was
notified. Audio is not approved or integrated by this logic delivery. No merge,
PR creation, build, PCK export, game run, initialization or deployment was done.
The release manifest is unchanged.

## Static Verification

- `validate_b140_balance.py`: 11 source-contract/rules-model tests; 36 broken
  source variants rejected. The Python model is not execution of production C#.
- `validate_issue_198.py`: PASS, Getter-card exhaustion scope unchanged.
- `validate_saint_dragon_naming.py`: PASS, model/localization/save identity retained.
- `validate_issue_47.py`: PASS.
- `validate_b130_fighting_feedback.py`: 10 tests PASS; original damage source read-only.
- `validate_b1_1_0.py`: PASS with approved Chosen One balance pin.
- `validate_b130_core.py`: 507 source checks PASS.
- All 52 tracked JSON files parse; working-tree diff-check PASS.
- `validate_issue_10.py`: FAIL at its existing Tactical Retreat distance/animation
  contract. Both the validator and `ShinGetterCombatVfx.cs` are byte-identical to
  `origin/main` in this delivery. This unrelated baseline failure is retained,
  not fixed or suppressed; do not report the entire historical suite as passing.

Independent read-only hook analysis identified the Harmony priority sentinel;
the implementation uses `-2` and preserves native modifier enumeration.
Actual Harmony binding, damage, save round trips, UI wrapping, multiplayer and
gameplay still require main-thread review and target-line validation. Audio
approval remains a separate Artanis decision.
