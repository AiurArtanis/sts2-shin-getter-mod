#!/usr/bin/env python3
"""Static 0.111 Beta compatibility and release-mapping gate for issue#93."""

from __future__ import annotations

import hashlib
import json
import os
import re
import subprocess
import sys
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = PROJECT_ROOT.parent
BETA_SOURCE = Path(
    os.environ.get("SHIN_GETTER_STS2_111_SOURCE", r"E:\Work\SlaytheSpare2-111-beta")
)

FORMAL_VERSION = "v1.2.0"
FORMAL_TAG = "mod-v1.2.0"
FORMAL_ARCHIVE = "shin-getter-mod-v1.2.0.zip"
FORMAL_URL = (
    "https://github.com/AiurArtanis/sts2-shin-getter-mod/releases/tag/mod-v1.2.0"
)
BETA_VERSION = "v1.2.0-beta.111"
BETA_TAG = "mod-v1.2.0-beta.111"
BETA_ARCHIVE = "shin-getter-mod-v1.2.0(111-beta).zip"
BETA_URL = (
    "https://github.com/AiurArtanis/sts2-shin-getter-mod/releases/tag/"
    "mod-v1.2.0-beta.111"
)
BETA_HISTORY_KEY = "SHIN_GETTER_CHUNIBYO.UPDATE.v1_2_0_beta_111"

RELEASE_FILES = (
    REPO_ROOT / "README.md",
    REPO_ROOT / "README_EN.md",
    REPO_ROOT / "README_JP.md",
    REPO_ROOT / "workshop/WORKSHOP_DESCRIPTION_BBCODE.txt",
    REPO_ROOT / "workshop/WORKSHOP_DESCRIPTION_EN_BBCODE.txt",
    REPO_ROOT / "workshop/WORKSHOP_DESCRIPTION_JP_BBCODE.txt",
)

POWER_DAMAGE_OVERRIDES = (
    "SGP_Airborne.cs",
    "SGP_HotBlood.cs",
    "SGP_IronWall.cs",
    "SGP_Ki.cs",
    "SGP_OpenGet.cs",
    "SGP_Radiation.cs",
    "SGP_Shade.cs",
    "SGP_Wane.cs",
)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def require(text: str, path: Path, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"{path}: missing issue#93 marker {needle!r}")


def reject(text: str, path: Path, *needles: str) -> None:
    for needle in needles:
        if needle in text:
            raise AssertionError(f"{path}: stale issue#93 marker remains {needle!r}")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def validate_version_mapping() -> None:
    manifest_path = PROJECT_ROOT / "ShinGetterMod.json"
    manifest = json.loads(read(manifest_path))
    expected = {
        "version": BETA_VERSION,
        "min_game_version": "0.111.0",
        "has_pck": True,
        "has_dll": True,
    }
    for key, value in expected.items():
        if manifest.get(key) != value:
            raise AssertionError(f"{manifest_path}: {key}={manifest.get(key)!r}, expected {value!r}")

    history_path = PROJECT_ROOT / "ShinGetterMod/update_history.json"
    history = json.loads(read(history_path))
    expected_latest = {
        "version": BETA_VERSION,
        "date": "2026-08-26",
        "localization_key": BETA_HISTORY_KEY,
    }
    if not history or history[0] != expected_latest:
        raise AssertionError(f"{history_path}: Beta history entry must be first: {history[:1]}")
    if sum(entry.get("version") == BETA_VERSION for entry in history) != 1:
        raise AssertionError(f"{history_path}: expected exactly one {BETA_VERSION} entry")
    if sum(entry.get("version") == FORMAL_VERSION for entry in history) != 1:
        raise AssertionError(f"{history_path}: formal {FORMAL_VERSION} history regressed")

    for language in ("zhs", "eng", "jpn"):
        path = PROJECT_ROOT / f"ShinGetterMod/localization/{language}/settings_ui.json"
        table = json.loads(read(path))
        require(table.get(BETA_HISTORY_KEY, ""), path, "0.111", FORMAL_VERSION)

    for path in RELEASE_FILES:
        text = read(path)
        require(
            text,
            path,
            FORMAL_VERSION,
            FORMAL_TAG,
            FORMAL_ARCHIVE,
            FORMAL_URL,
            BETA_VERSION,
            BETA_TAG,
            BETA_ARCHIVE,
            BETA_URL,
            "0.111",
        )
        reject(text, path, "0.110 Beta", "support-110-beta")


def validate_mod_api_adaptation() -> None:
    source_root = PROJECT_ROOT / "src"
    source_files = list(source_root.rglob("*.cs"))
    all_source = "\n".join(read(path) for path in source_files)
    current = re.findall(r"\.FromCard\(this,\s*cardPlay\)", all_source)
    stale = re.findall(r"\.FromCard\(this\)(?!\s*,)", all_source)
    if len(current) != 63 or stale:
        raise AssertionError(
            f"AttackCommand.FromCard migration drifted: current={len(current)}, stale={len(stale)}"
        )

    character_path = source_root / "Models/Characters/ShinGetter.cs"
    require(
        read(character_path),
        character_path,
        "GenerateAnimator(MegaSprite controller, Creature creature)",
    )

    power_root = source_root / "Models/Powers"
    for name in POWER_DAMAGE_OVERRIDES:
        path = power_root / name
        text = read(path)
        require(text, path, "CardPlay? cardPlay")
        if not re.search(r"override decimal ModifyDamage(?:Additive|Multiplicative)\(", text):
            raise AssertionError(f"{path}: expected damage override is missing")

    required_mod_markers = {
        source_root / "Nodes/Combat/NShinGetterFormChoice.cs": (
            "SignalPlayerChoiceBegun(player, PlayerChoiceOptions.None)",
        ),
        source_root / "Patches/ShinGetterVisualsPatch.cs": (
            "StartRunLobbyPlayer player",
        ),
        source_root / "Patches/ShinGetterPotionFactoryWeightPatch.cs": (
            '[HarmonyPatch(typeof(PotionFactory), "CreateRandomPotions")]',
            "ref IEnumerable<PotionModel> __result",
        ),
        source_root / "Models/Cards/SGC_Avalanche.cs": (
            "CreatureCmd.LoseBlock(choiceContext, Owner.Creature, consumedBlock, null)",
        ),
        source_root / "Models/Cards/SGC_GetterChop.cs": (
            "PlunderShield(PlayerChoiceContext choiceContext, CardPlay cardPlay)",
            "CreatureCmd.LoseBlock(choiceContext, cardPlay.Target, stolenBlock, Owner.Creature)",
        ),
        source_root / "Models/Enchantments/SGE_Adaptation.cs": ("Card,\n            cardPlay",),
        source_root / "Models/Cards/SGC_GetterMissile.cs": ("this,\n                cardPlay",),
        source_root / "Models/Cards/SGC_PetalBreakthrough.cs": ("this,\n            cardPlay",),
        source_root / "Models/Cards/SGC_Radiated.cs": ("this,\n                cardPlay",),
        source_root / "Models/Cards/SGC_SpiralDrill.cs": ("damageProps, this, cardPlay",),
    }
    for path, markers in required_mod_markers.items():
        require(read(path), path, *markers)

    reject(
        read(source_root / "Patches/ShinGetterVisualsPatch.cs"),
        source_root / "Patches/ShinGetterVisualsPatch.cs",
        "        LobbyPlayer player",
    )
    reject(
        read(source_root / "Patches/ShinGetterPotionFactoryWeightPatch.cs"),
        source_root / "Patches/ShinGetterPotionFactoryWeightPatch.cs",
        '"CreateRandomPotion")',
        "ref List<PotionModel> __result",
    )


def validate_beta_source_contract() -> None:
    if not (BETA_SOURCE / ".codegraph").exists():
        raise AssertionError(f"Beta CodeGraph index is unavailable: {BETA_SOURCE / '.codegraph'}")

    contracts = {
        "src/Core/Models/CharacterModel.cs": (
            "GenerateAnimator(MegaSprite controller, Creature creature)",
        ),
        "src/Core/Models/AbstractModel.cs": (
            "ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)",
            "ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)",
        ),
        "src/Core/Commands/Builders/AttackCommand.cs": (
            "FromCard(CardModel card, CardPlay? cardPlay)",
        ),
        "src/Core/Commands/CreatureCmd.cs": (
            "Damage(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)",
            "LoseBlock(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? remover)",
        ),
        "src/Core/GameActions/Multiplayer/PlayerChoiceContext.cs": (
            "SignalPlayerChoiceBegun(Player chooser, PlayerChoiceOptions options)",
        ),
        "src/Core/Nodes/Screens/CharacterSelect/NCharacterSelectScreen.cs": (
            "PlayerChanged(StartRunLobbyPlayer player, bool isRandomCharacterResolution)",
        ),
        "src/Core/Hooks/Hook.cs": (
            "CardModel? cardSource, CardPlay? cardPlay, ModifyDamageHookType modifyDamageHookType",
            "item.ModifyDamageAdditive(target, num, props, dealer, cardSource, cardPlay)",
            "item2.ModifyDamageMultiplicative(target, num, props, dealer, cardSource, cardPlay)",
        ),
        "src/Core/Factories/PotionFactory.cs": (
            "private static IEnumerable<PotionModel> CreateRandomPotions(IEnumerable<PotionModel> options, int count, Rng rng)",
        ),
        "src/Core/Modding/ModInitializerAttribute.cs": (
            "public class ModInitializerAttribute : Attribute",
            "public ModInitializerAttribute(string initializerMethod)",
        ),
        "src/Core/Modding/ModManager.cs": (
            "Path.Combine(mod.path, modId + \".dll\")",
            "Path.Combine(mod.path, modId + \".pck\")",
            "ProjectSettings.LoadResourcePack(text3)",
            "CallModInitializer(item)",
        ),
    }
    for relative, markers in contracts.items():
        path = BETA_SOURCE / relative
        require(read(path), path, *markers)

    reflection_contracts = {
        "src/Core/Models/PowerModel.cs": ("_internalData",),
        "src/Core/Models/Powers/VigorPower.cs": (
            "commandToModify",
            "amountWhenAttackStarted",
        ),
        "src/Core/Nodes/Screens/CardLibrary/NCardLibrary.cs": (
            "_poolFilters",
            "_cardPoolFilters",
            "UpdateCardPoolFilter",
        ),
        "src/Core/Nodes/Screens/CardLibrary/NCardLibraryGrid.cs": (
            "_allCards",
            "GetCardVisibility",
        ),
        "src/Core/Nodes/Cards/NCard.cs": ("_frame", "Reload", "UpdateVisuals"),
        "src/Core/Models/Events/TheArchitect.cs": (
            "_architectCreature",
            "_score",
            "_speechBubble",
            "AnimPlayerAttackIfNecessary",
        ),
        "src/Core/Nodes/Screens/GameOverScreen/NGameOverScreen.cs": (
            "_localPlayer",
            "_history",
            "_deathQuote",
        ),
        "src/Core/Nodes/Combat/NPowerContainer.cs": ("_powerNodes", "UpdatePositions"),
        "src/Core/Nodes/Screens/CharacterSelect/NCharacterSelectScreen.cs": ("_bgContainer",),
        "src/Core/Nodes/Screens/CharacterSelect/NMultiplayerLoadGameScreen.cs": ("_bgContainer",),
    }
    for relative, markers in reflection_contracts.items():
        path = BETA_SOURCE / relative
        require(read(path), path, *markers)

    beta_dll = BETA_SOURCE / ".godot/mono/temp/bin/Debug/sts2.dll"
    harmony_dll = BETA_SOURCE / ".godot/mono/temp/bin/Debug/0Harmony.dll"
    expected_hashes = {
        beta_dll: "6896BBA91CEDDC661B3F789749E9F0AAC338F5DDBBB92C598FC344DEC822DC19",
        harmony_dll: "EF1898322C9F5C86DC1B0758B272A9C440823B4A41CA9A0B82A3AA6B3D206387",
    }
    for path, expected_hash in expected_hashes.items():
        actual = sha256(path)
        if actual != expected_hash:
            raise AssertionError(f"{path}: assembly hash {actual} != audited {expected_hash}")


def validate_resources_and_audit() -> None:
    entry_path = PROJECT_ROOT / "src/Entry.cs"
    require(
        read(entry_path),
        entry_path,
        '[ModInitializer("Init")]',
        "public static void Init()",
        "ShinGetterMod - loading success! (77 cards)",
    )

    project_path = PROJECT_ROOT / "project.godot"
    require(
        read(project_path),
        project_path,
        'config/features=PackedStringArray("4.5", "C#", "GL Compatibility")',
        'project/assembly_name="ShinGetterMod"',
    )

    resources = (
        "ShinGetterMod/update_history.json",
        "scenes/screens/char_select/char_select_bg_shin_getter.tscn",
        "materials/transitions/shin_getter_transition_mat.tres",
        "images/atlases/power_atlas.sprites/s_g_p_shin_getter_one.tres",
        "images/characters/shin_getter/forms/getter_one_idle/sprite_sheet.png",
        "ShinGetterMod/localization/zhs/settings_ui.json",
        "ShinGetterMod/localization/eng/settings_ui.json",
        "ShinGetterMod/localization/jpn/settings_ui.json",
    )
    missing = [relative for relative in resources if not (PROJECT_ROOT / relative).is_file()]
    if missing:
        raise AssertionError(f"Critical Beta resources are missing: {missing}")

    audit_path = REPO_ROOT / ".github/issue-93-111-beta-api-audit.md"
    audit = read(audit_path)
    require(
        audit,
        audit_path,
        "377 game type references",
        "698 direct game member signatures",
        "33 members on constructed generic game types",
        "Exactly 26 changed symbol groups intersect the mod",
        "0 warnings and 10 errors",
        BETA_VERSION,
        BETA_TAG,
        BETA_ARCHIVE,
    )


def validate_codegraph_mechanical_audit() -> None:
    script = PROJECT_ROOT / "tools/audit_issue_93_codegraph.py"
    result = subprocess.run(
        [sys.executable, str(script), "--check"],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        details = "\n".join((result.stdout + "\n" + result.stderr).splitlines()[-30:])
        raise AssertionError(f"0.109 -> 0.111 CodeGraph audit gate failed:\n{details}")
    require(
        result.stdout,
        script,
        "issue#93 CodeGraph audit check passed",
        "26 changed-symbol candidates",
    )


def validate_runtime_targets() -> None:
    project = REPO_ROOT / "tools/Issue93CompatibilityProbe/Issue93CompatibilityProbe.csproj"
    environment = os.environ.copy()
    environment["SHIN_GETTER_STS2_111_SOURCE"] = str(BETA_SOURCE)
    result = subprocess.run(
        ["dotnet", "run", "--project", str(project), "--configuration", "Release"],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        details = "\n".join((result.stdout + "\n" + result.stderr).splitlines()[-30:])
        raise AssertionError(f"0.111 Beta runtime target probe failed:\n{details}")
    require(
        result.stdout,
        project,
        "issue#93 0.111 Beta runtime reflection/Harmony target probe passed",
    )


def main() -> None:
    validate_version_mapping()
    validate_mod_api_adaptation()
    validate_beta_source_contract()
    validate_resources_and_audit()
    validate_codegraph_mechanical_audit()
    validate_runtime_targets()
    print("issue#93 0.111 Beta compatibility validation passed")


if __name__ == "__main__":
    main()
