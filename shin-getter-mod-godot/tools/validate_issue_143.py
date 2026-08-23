#!/usr/bin/env python3
"""Static regression gate for issue#143."""

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    patch = ROOT / "src/Patches/ShinGetterSpiritCommandRetainPatch.cs"
    if not patch.is_file():
        raise AssertionError("Missing ShinGetterSpiritCommandRetainPatch.cs")

    text = patch.read_text(encoding="utf-8-sig")
    for fragment in (
        'HarmonyPatch(typeof(CardModel), "get_ShouldRetainThisTurn")',
        "__instance is not ShinGetterCardBase { SpiritRequirement: > 0 }",
        "spiritCommand.CombatState == null",
        "spiritCommand.Owner.Creature.GetPower<SGP_Ki>()?.Amount > 0",
    ):
        if fragment not in text:
            raise AssertionError(f"Missing issue#143 assertion: {fragment}")

    card_base = (ROOT / "src/Models/Cards/ShinGetterCardBase.cs").read_text(
        encoding="utf-8-sig"
    )
    for fragment in (
        '["SGC_Ki"] = new[] { "气力", "活力", "精神指令卡" }',
        'ContextualHoverTipExclusions',
        '["SGC_Insight"] = new HashSet<string>',
        '["SGC_Desperation"] = new HashSet<string>',
        '"一号机", "二号机", "三号机"',
        '.Where(term => excludedTerms?.Contains(term) != true)',
    ):
        if fragment not in card_base:
            raise AssertionError(f"Missing issue#143 hover-tip assertion: {fragment}")

    icon = (
        "[img=top,22x22]res://images/atlases/power_atlas.sprites/"
        "s_g_p_ki.tres[/img]"
    )
    expected_descriptions = {
        "zhs": f"卡面带有{icon}标记的卡牌。拥有气力时，获得保留；达到指定数量后，该牌的耗能降低1。",
        "eng": f"A card marked with {icon}. While you have Ki, it gains Retain. If your current Ki meets its requirement, it costs 1 less.",
        "jpn": f"カード面に{icon}マークがあるカード。気力がある間は保留を得る。現在の気力が指定数以上なら、コストが1減る。",
    }
    for locale, expected in expected_descriptions.items():
        path = ROOT / "ShinGetterMod/localization" / locale / "static_hover_tips.json"
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        actual = data.get("SHIN_GETTER_SPIRIT_COMMAND.description")
        if actual != expected:
            raise AssertionError(f"{path}: expected updated spirit-command tip")

    print("issue#143 static validation passed")


if __name__ == "__main__":
    main()
