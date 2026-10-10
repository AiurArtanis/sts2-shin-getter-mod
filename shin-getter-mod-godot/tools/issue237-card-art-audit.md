# issue#237 compact-v1 card-art integration

## Scope

- Approved development start: main `b0f76260baba42d33952c28dff5fb66980e2efbc`.
- Resource branch: `feat/issue-237-card-art-compact-20261006`.
- Inputs are the approved card-production handoff, not newly generated artwork.
- Eight replaced1000×760 portraits: GetterClaw, GetterRush, GetterTomahawk, Guts,
  HurricaneStrike, LigerAssault, SaotomeBlueprint, SuperKi. Their byte hashes match
  the supplied PNGs; all are8-bit indexed with no alpha/tRNS.
- One2500×2470 RGB atlas,10 columns ×13 rows of250×190 cells, zero padding/gaps.
- Fixed sequence positions;67/68/72/77 contain the user's approved common
  three-machine placeholder, not real ancient portraits. Other cards never shift.
- All76 AtlasTexture files referencing this sheet are migrated, not only the73
  currently used ordinary consumers. Other SpriteFrames/fonts/item atlases are untouched.

## All-reference migration

The73 delivered resources preserve every field except region/filter_clip.
Three unused legacy resources are also migrated to their explicit sequence slots:

| File | Sequence | New region |
| --- | ---: | --- |
| s_g_c_getter_landing.tres |77 |Rect2(1500,1330,250,190) |
| s_g_c_shin_form.tres |68 |Rect2(1750,1140,250,190) |
| s_g_c_stoner_shine.tres |67 |Rect2(1500,1140,250,190) |

The historical Landing region was outside the old sheet; it is not used to infer
the new sequence. These legacy files remain unused; no C# consumer is switched to
them. The4 ancient classes retain their independent card_single PNG paths and rarity.
All76 have filter_clip=true. Existing import UIDs/remaps/mipmap parameters are
unchanged. Text assets use the repository's LF policy; image hashes remain byte-exact.

## Reproducible gates

```text
python tools/validate_issue_237.py
python tools/test_issue_237_gate.py
python tools/validate_issue_89.py
```

`issue237-card-art-manifest.json` is portable and contains resource-relative paths,
approved image/text hashes,130 RGB-cell hashes,76 exact texture mappings and78
preserved-asset hashes. It includes no account/vault/source-asset absolute paths.

- RED observed against the old sheet: expected2500×2470 size mismatch.
- GREEN:9 PNG,76 resources (73 live +3 unused),130 exact RGB cells.
- Positive temporary fixture plus16 rejected mutations: old padding; unmigrated
  unused resource; missing/extra refs; wrong atlas path; disabled clipping; wrong
  frame size; old dimensions; RGBA even when opaque; changed atlas pixels; portrait
  swap; broken PNG CRC; modified import mipmaps; changed independent ancient image;
  ancient consumer redirected to atlas; obsolete issue#89 size expectation.
- issue#89's four card-coordinate assertions and atlas dimensions are updated to
  the new authoritative layout, not removed. Relic/potion expectations are unchanged.
- Relevant unchanged-source checks: issue#7, issue#10, issue#143, issue#166 and
  Saint Dragon naming PASS.568 UID declarations/0 duplicates; no import/UID edits.
- Final source-branch sweep: all32 validate_*.py scripts PASS. The official-source
  option for issue#206,16 card-resource rejection fixtures and4 priority rejection
  fixtures were also run separately. No engine/native/GPU execution is implied.
- Full JSON count is45 including this new manifest. Original malformed Japanese
  dialogue was identified as an inherited main-base defect, not a card-art regression.

## Narrow base maintenance, separate commits

Main developer explicitly authorized only the already existing minimal corrections:

1. `1006222807afa627afd930900891bed5056d80d7`, cherry-picked with source annotation as
   `c935d9fcc7f497ef65ce46fcaf934670c5e4a3d5`: cryptographic RNG alias and the already
   loaded save's nonnull local in SelectEncounter (7 additions/5 deletions in1 C# file).
2. `eb174b823fa9d9e479b8c6b8c8cba6986af9de9e`, cherry-picked separately as
   `47593bd5e8e5f2e2b5ac4e542ff39e623cfabfcc`: removes only1 extra final bracket in
   jpn.json; no translated text changes.

Before these corrections the unmodified inherited BondSession produced2 RNG errors
and1 nullable warning; Japanese JSON was44/45 parseable. After the narrow fixes the
formal109 developer build is0 warnings/0 errors and45/45 JSON parse.
No sgd feature candidate, B1 preview, other C# adjustments or whole-branch merge is
included. The original main starting SHA remains explicit and the corrections are
separate from the resource commit.

The old issue#206 structural gate initially failed with ValueError on its obsolete
`_save!` literal. Main approved synchronization to the nonnull local `save`, scoped
only to SelectEncounter's body (ending at ReadReliableHistories). Loaded local,
first/bonds/absence/latest-result order and explicit cryptographic RNG alias are
strictly checked; all other persistence/mode/migration/API checks are retained.
Positive source plus4 rejected static fixtures cover deleted/late initialization,
swapped first/Choices and a wrong RNG alias. The full issue#206 gate with official109
source-root passes. These are static contracts, not native dialogue acceptance.

## Not performed

No Godot/game/native automation, fresh resource import, GPU sampling/visual acceptance,
PCK export/initialization, shared deployment, PR/merge, release or issue closure.
Source PNG/tres checks do not prove imported-resource rendering or artwork readability.
Root checkout, existing user modifications, B1 preview, daily Godot/Steam, original
game source and Beta are outside this branch's writes. Gameplay/localization apart
from the approved single Japanese bracket correction is unchanged.
