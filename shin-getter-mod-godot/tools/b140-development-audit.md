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

## 2026-11-08 Approved Voice Integration

The November 6 audio boundary above is historical. Artanis approved the four
masters in voice chat `01a159be-6ee4-76a1-9d76-b0af8b7bd9e6`; the authoritative
handoff is `musashi-hat-issue270-20261108-approved/approval-final.json`.
Ryoma V9, Hayato V9, Benkei V7 and the existing trio V5 are copied byte-for-byte
as WAVs. No regeneration, remix, trimming, resampling, normalization or Vorbis
conversion was performed. Their new Godot WAV imports keep uncompressed PCM.
`tools/issue270-approved-audio.json` pins hashes, translations and runtime paths.
The old Hayato sentence about Musashi having died years ago is not used.

Workbook codes 068-071 are already reserved for unrelated lines, so the four
new entries are 072-075. They use null persistent cues: the saved relic combat
charge already controls actual activation. Existing 64 cue values, low/high
history masks and save fields stay byte-identical. Silent mode suppresses the
voice and subtitle but not the relic effect. Always and OncePerCombat both
allow one line per actual per-owner relic activation, not a new global speech
history slot. The voice uses the captured trigger form and is non-blocking.

FatalDamageResponse interrupts an older voice, then occupies the existing
damage-response slot. Ordinary first-hit/no-HP-loss lines therefore do not cut
off the new hat line. Cleanup uses existing per-owner audio/subtitle lifecycle;
the complete approved master is played without subtitle-driven audio slicing.
Audio errors are logged without preventing the relic's reward commands.
New subtitles have red/white/yellow/Getter-ray text and jitter only on Musashi's
name. Debug `sgs` and the resource-probe inventory include all four new codes.

The subtitle workbook was edited with OfficeCLI 1.0.160 after an exclusive-open
check and byte-identical backup:
`D:/Library/Pictures/杀戮尖塔2-素材/sound/音频字幕.before-issue270-20261108.xlsx`.
Backup SHA-256:
`2C42D958F14EF2840292AE2EC245118CECB5A8DA45B0E562FE897FEAAF65A060`.
Only `完整设计!A73:H76` was added, with four new row heights set to 150pt.
All 1,171 existing cell values/formats across three sheets were compared to
the backup and preserved. No worksheet or column was added. Japanese scripts,
Chinese subtitles, forms, actors, approved versions and resource paths fit the
existing columns. IDs 072-075 occur exactly once each.

OfficeCLI initially found three description-cell height overflows; only the
four new row heights were corrected. The final issue count is zero, schema
validation passes, and all five Excel error queries return zero. HTML was
regenerated and all three sheet tabs inspected in Ego Lite. New description
cells report `clientHeight == scrollHeight == 200px` with wrapping enabled.
OfficeCLI PNG capture failed for lack of a working headless target, and Ego
PNG capture was blocked by a minimized window. No screenshot/pixel validation
is claimed; HTML/DOM, worksheet contents and native OfficeCLI checks are the
actual verification. The browser task was finished without leaving its tabs.

New tests: `validate_issue_270_voice.py` has six asset/source tests and rejects
11 voice-wiring mutations. The B1.4.0 balance rules and legacy voice gates are
retained and adapted only for this now-approved integration. Game playback,
in-engine formatting and target-line import/build validation remain pending.
No PR, main merge, build, PCK, game initialization, deployment or release is
part of this branch delivery.

Final workbook: 38,412 bytes, SHA-256
`49EEC3E8FECAF6FEB9A7A39F1FCAD1FE7EA9E842708F7A5702119CBD1CC398D0`.
Final OfficeCLI schema/issue checks pass after the height correction; all
1,171 original cells were compared again after closing/flushing the workbook.
The external receipt is `sound/issue270-subtitles-20261108/workbook-receipt.json`.

Final static run passed `validate_issue_270_voice.py`,
`validate_b140_balance.py`, `validate_issue_21_31.py`, `validate_issue_124.py`
(147 mapped frames, 63 rejected variants), `validate_issue_169.py`,
`validate_b1_1_0.py` and `validate_b130_core.py` (507 source checks).
FFmpeg decoded all four WAVs without errors; `git diff --check` passed.
The unchanged issue#10 baseline failure documented above is not suppressed
or reclassified as a pass.
