from pathlib import Path
import re
import sys


ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
PATCH = ROOT / "src/Patches/ShinGetterEventCharacterVisualPatch.cs"
SCENE = ROOT / "scenes/creature_visuals/shin_getter.tscn"
MERCHANT = ROOT / "scenes/merchant/characters/shin_getter_merchant.tscn"
VOICE = ROOT / "src/Audio/ShinGetterVoiceService.cs"
MUSIC = ROOT / "src/Audio/ShinGetterEncounterMusicService.cs"


def require(text: str, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"missing required issue#195 guard: {needle}")


patch = PATCH.read_text(encoding="utf-8")
scene = SCENE.read_text(encoding="utf-8")
merchant = MERCHANT.read_text(encoding="utf-8")

require(
    patch,
    "using MegaCrit.Sts2.Core.Helpers;",
    '[HarmonyPatch(typeof(NFakeMerchant), "StartCharacterAnimation")]',
    "TryShowRyoma(visuals)",
    'GetNodeOrNull<AnimatedSprite2D>("GetterOne")',
    "s_g_o_merchant_ryoma_normal.png",
    '[HarmonyPatch(typeof(TheArchitect), nameof(TheArchitect.OnRoomEnter))]',
    "ShinGetterForm.Getter1",
    "animate: false",
)
require(scene, 'animation = &"fusion"', '[node name="GetterOne" type="AnimatedSprite2D"')
require(merchant, "s_g_o_merchant_ryoma_normal.png")

if "PlayOpeningGetterOneFusion" in patch:
    raise AssertionError("Architect event visuals must not play the combat-opening fusion.")

failures = []
if "body.Hide();" not in patch or "visuals.AddChildSafely(ryoma);" not in patch:
    failures.append("fake merchant must hide the complete fallback body and attach Ryoma as a sibling")
if "body.SelfModulate =" in patch or "body.AddChildSafely(ryoma)" in patch:
    failures.append("fallback ERROR texture must never be made opaque below Ryoma")
for guard in (
    "Vector2.One / container.Scale",
    "Vector2 portraitScale = layoutCompensation * FakeMerchantRyomaScaleMultiplier",
    "new Vector2(0f, -193.576f) * portraitScale",
    "new Vector2(0.376f, 0.376f) * portraitScale",
    "visuals.Bounds.Position = ryoma.Position + spriteRect.Position * ryoma.Scale",
    "visuals.Bounds.Size = spriteRect.Size * ryoma.Scale",
):
    if guard not in patch:
        failures.append(f"missing merchant layout guard: {guard}")
voice = VOICE.read_text(encoding="utf-8")
opening = voice.split("private static void PlayCombatStart(", 1)[1].split(
    "internal static void ResetCombatVoiceHistory", 1
)[0]
if "room.ParentEventId != null" not in opening:
    failures.append("event-origin combat voice must not rely only on Unknown map points")
if "MapPointType.Unknown" not in opening:
    failures.append("existing Unknown-node opening voice behavior must remain")
require(
    MUSIC.read_text(encoding="utf-8"),
    "category = room.ParentEventId != null",
    "? ShinGetterBgmCategory.EventCombat",
    "ShinGetterChunibyoConfigService.GetBgmTrackId(category)",
    "configured.Id != ShinGetterBgmCatalog.DefaultTrackId",
    "ShinGetterBgmCatalog.ResolveForPlayback(configured).ResourcePath",
)

# Preserve container compensation, then apply the event-only art-direction adjustment.
match = re.search(r"const float FakeMerchantRyomaScaleMultiplier = ([0-9.]+)f;", patch)
if match is None or float(match.group(1)) != 1.1:
    failures.append("feedback3 requires a bounded event-only 10 percent portrait increase")
multiplier = float(match.group(1)) if match else 1.0
require(merchant, "position = Vector2(0, -193.576)", "scale = Vector2(0.376, 0.376)")
for container_scale in (1.0, 1.75, 2.0):
    effective_scale = 0.376 / container_scale * multiplier * container_scale
    effective_offset = -193.576 / container_scale * multiplier * container_scale
    assert abs(effective_scale - 0.376 * multiplier) < 1e-9
    assert abs(effective_offset + 193.576 * multiplier) < 1e-9
    assert abs(effective_offset / effective_scale - (-193.576 / 0.376)) < 1e-9

if failures:
    raise AssertionError("issue#195 feedback2/3 regressions:\n- " + "\n- ".join(failures))

print("issue#195 static regression: PASS")
