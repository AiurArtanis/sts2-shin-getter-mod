import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
KEY = "S_G_C_HOLY_DRAGON_ROAR.description"
EXPECTED_PREFIXES = {
    "zhs": "消耗所有[gold]盖塔卡牌[/gold]。",
    "eng": "Exhaust all [gold]Getter cards[/gold].",
    "jpn": "すべての[gold]ゲッターカード[/gold]を廃棄する。",
}
FORBIDDEN = {
    "zhs": ("手牌",),
    "eng": ("in your hand",),
    "jpn": ("手札",),
}

for language, expected_prefix in EXPECTED_PREFIXES.items():
    path = ROOT / f"ShinGetterMod/localization/{language}/cards.json"
    description = json.loads(path.read_text(encoding="utf-8"))[KEY]
    if not description.startswith(expected_prefix):
        raise AssertionError(f"issue#198: unexpected {language} Holy Dragon Roar scope")
    for forbidden in FORBIDDEN[language]:
        if forbidden in description:
            raise AssertionError(f"issue#198: stale hand-only wording in {language}: {forbidden}")
    for token in ("{Damage:diff()}", "{BurnDamage:diff()}", "[gold]", "[/gold]"):
        if token not in description:
            raise AssertionError(f"issue#198: missing {language} token: {token}")

card = (ROOT / "src/Models/Cards/SGC_HolyDragonRoar.cs").read_text(encoding="utf-8")
for pile in ("PileType.Draw", "PileType.Hand", "PileType.Discard"):
    if pile not in card:
        raise AssertionError(f"issue#198: runtime scope no longer includes {pile}")

print("issue#198 static regression: PASS")
