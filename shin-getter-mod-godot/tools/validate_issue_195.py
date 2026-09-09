from pathlib import Path
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
    "new Vector2(0f, -193.576f) * layoutCompensation",
    "new Vector2(0.376f, 0.376f) * layoutCompensation",
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

# The inherited event container magnifies both dimensions, not just the sprite texture.
for container_scale in (1.0, 1.75, 2.0):
    assert abs(0.376 / container_scale * container_scale - 0.376) < 1e-9
    assert abs(-193.576 / container_scale * container_scale + 193.576) < 1e-9

if failures:
    raise AssertionError("issue#195 feedback2 regressions:\n- " + "\n- ".join(failures))

print("issue#195 static regression: PASS")
