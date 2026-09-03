from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PATCH = ROOT / "src/Patches/ShinGetterEventCharacterVisualPatch.cs"
SCENE = ROOT / "scenes/creature_visuals/shin_getter.tscn"
MERCHANT = ROOT / "scenes/merchant/characters/shin_getter_merchant.tscn"


def require(text: str, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"missing required issue#195 guard: {needle}")


patch = PATCH.read_text(encoding="utf-8")
scene = SCENE.read_text(encoding="utf-8")
merchant = MERCHANT.read_text(encoding="utf-8")

require(
    patch,
    '[HarmonyPatch(typeof(NFakeMerchant), "StartCharacterAnimation")]',
    "TryShowRyoma(visuals)",
    'GetNodeOrNull<AnimatedSprite2D>("GetterOne")',
    'GetNodeOrNull<CanvasItem>("GetterOne")?.Hide()',
    'GetNodeOrNull<CanvasItem>("GetterTwo")?.Hide()',
    'GetNodeOrNull<CanvasItem>("GetterThree")?.Hide()',
    'GetNodeOrNull<CanvasItem>("ShinDragon")?.Hide()',
    "s_g_o_merchant_ryoma_normal.png",
    '[HarmonyPatch(typeof(TheArchitect), nameof(TheArchitect.OnRoomEnter))]',
    "ShinGetterForm.Getter1",
    "animate: false",
)
require(scene, 'animation = &"fusion"', '[node name="GetterOne" type="AnimatedSprite2D"')
require(merchant, "s_g_o_merchant_ryoma_normal.png")

if "PlayOpeningGetterOneFusion" in patch:
    raise AssertionError("Architect event visuals must not play the combat-opening fusion.")

print("issue#195 static regression: PASS")
