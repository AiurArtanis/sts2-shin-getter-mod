"""Mechanical merge of reviewed, authored issue#238 strings. --check does not write."""
from pathlib import Path
import argparse
import json
import re

ROOT = Path(__file__).resolve().parents[1]
source = json.loads((ROOT / "tools/issue238-localization.json").read_text(encoding="utf-8-sig"))
check = argparse.ArgumentParser()
check.add_argument("--check", action="store_true")
check.add_argument("--normalize-glossary", action="store_true")
args = check.parse_args()
if args.normalize_glossary:
    # Reviewed against the already shipped card titles / native 109 enchantment table.
    replacements = [("超级气势", "超强气"), ("Insight", "Foresight"), ("Indomitable", "Persist"),
                    ("Iron Wall", "Wall"), ("Guts", "Prevail"), ("Super Ki", "Super Spirit"),
                    ("Requires Spirit", "Requires Drive"), ("予備の策", "予備プラン"),
                    ("スーパー気合", "超強気"), ("魂の力", "魂力"), ("軽快", "身軽")]
    def normalize(value):
        if isinstance(value, str):
            for before, after in replacements:
                value = value.replace(before, after)
            return value
        if isinstance(value, list): return [normalize(v) for v in value]
        if isinstance(value, dict): return {k: normalize(v) for k, v in value.items()}
        return value
    source = normalize(source)
    (ROOT / "tools/issue238-localization.json").write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("normalized titles against shipped glossary")
extra_locked = {
    "WHISPERING_HOLLOW": {
        "BENKEI_FORM": ["[yellow]弁庆：[/yellow]「这风要真波塞冬才能接。」", "[yellow]Benkei:[/yellow]「This wind needs Shin Poseidon to catch it.」", "[yellow]弁慶：[/yellow]「この風は真ポセイドンでなきゃ受け止められねえ。」"],
        "BENKEI_OWNED": ["[yellow]弁庆：[/yellow]「这招已经记下了。」", "[yellow]Benkei:[/yellow]「We've already recorded this move.」", "[yellow]弁慶：[/yellow]「この技はもう記録してある。」"],
        "BENKEI_RELIC": ["[yellow]弁庆：[/yellow]「身上得有能留下的压舱物。」", "[yellow]Benkei:[/yellow]「We need something we can leave as ballast.」", "[yellow]弁慶：[/yellow]「重しにして置いていけるものが必要だ。」"]},
    "SYMBIOTE": {
        "BENKEI_MAXHP": ["[yellow]弁庆：[/yellow]最大生命需要高于5。", "[yellow]Benkei:[/yellow] Requires more than 5 Max HP.", "[yellow]弁慶：[/yellow]最大HPが5より多い必要がある。"],
        "BENKEI_TARGET": ["[yellow]弁庆：[/yellow]没有可附魔适应的攻击牌。", "[yellow]Benkei:[/yellow] No Attack can receive Adaptation.", "[yellow]弁慶：[/yellow]適応を付与できるアタックがない。"]},
    "AROMA_OF_CHAOS": {
        "HAYATO_ROLE": ["[white]隼人：[/white]需要识破／策略路线。", "[white]Hayato:[/white] Requires a Foresight / Strategy card.", "[white]隼人：[/white]見切り・戦略系のカードが必要。"],
        "HAYATO_TARGET": ["[white]隼人：[/white]没有可变化的牌。", "[white]Hayato:[/white] No card can be Transformed.", "[white]隼人：[/white]変化できるカードがない。"],
        "HAYATO_HP": ["[white]隼人：[/white]当前生命需要高于6。", "[white]Hayato:[/white] Requires more than 6 current HP.", "[white]隼人：[/white]現在HPが6より多い必要がある。"]},
    "HUNGRY_FOR_MUSHROOMS": {
        "BENKEI_ROLE": ["[yellow]弁庆：[/yellow]需要三号机／防杀路线。", "[yellow]Benkei:[/yellow] Requires a Getter 3 / defensive-offense card.", "[yellow]弁慶：[/yellow]ゲッター3・防御攻撃系のカードが必要。"],
        "BENKEI_HP": ["[yellow]弁庆：[/yellow]当前生命需要高于15。", "[yellow]Benkei:[/yellow] Requires more than 15 current HP.", "[yellow]弁慶：[/yellow]現在HPが15より多い必要がある。"],
        "BENKEI_TARGET": ["[yellow]弁庆：[/yellow]没有可升级的牌。", "[yellow]Benkei:[/yellow] No card can be Upgraded.", "[yellow]弁慶：[/yellow]アップグレードできるカードがない。"]},
    "FIELD_OF_MAN_SIZED_HOLES": {
        "HAYATO_ROLE": ["[white]隼人：[/white]「先看清洞口在量什么。」", "[white]Hayato:[/white]「First, find out what the holes are measuring.」", "[white]隼人：[/white]「まず、穴が何を測っているか見極める。」"],
        "HAYATO_HP": ["[white]隼人：[/white]需要大于7点生命。", "[white]Hayato:[/white] Requires more than 7 HP.", "[white]隼人：[/white]HPが7より多い必要がある。"],
        "HAYATO_TARGET": ["[white]隼人：[/white]「没有能被洞口咬住的战斗记录。」", "[white]Hayato:[/white]「No combat record for the holes to bite into.」", "[white]隼人：[/white]「穴に食いつかせる戦闘記録がない。」"]},
    "WATERLOGGED_VAULT": {
        "RYOMA_ROLE": ["[red]龙马：[/red]需要气魄。", "[red]Ryoma:[/red] Requires Spirit.", "[red]竜馬：[/red]気魄が必要。"],
        "RYOMA_HP": ["[red]龙马：[/red]当前生命不足；这次冲击会致死，需要大于8点生命。", "[red]Ryoma:[/red] This impact would be fatal. Requires more than 8 HP.", "[red]竜馬：[/red]この衝撃は致命傷になる。HPが8より多い必要がある。"]},
    "POTION_COURIER": {
        "RYOMA_GOLD": ["[red]龙马：[/red]需要60金币。", "[red]Ryoma:[/red] Requires 60 Gold.", "[red]竜馬：[/red]60ゴールドが必要。"],
        "RYOMA_SLOT": ["[red]龙马：[/red]需要1个空药水槽。", "[red]Ryoma:[/red] Requires an empty Potion slot.", "[red]竜馬：[/red]空のポーション枠が1つ必要。"],
        "RYOMA_POOL": ["[red]龙马：[/red]没有足够的罕见药水可供挑选。", "[red]Ryoma:[/red] Not enough distinct Uncommon Potions to choose from.", "[red]竜馬：[/red]選べるアンコモンポーションの種類が足りない。"],
        "HAYATO_GOLD": ["[white]隼人：[/white]需要70金币。", "[white]Hayato:[/white] Requires 70 Gold.", "[white]隼人：[/white]70ゴールドが必要。"],
        "HAYATO_RELIC": ["[white]隼人：[/white]「没有核对这瓶的资料。」", "[white]Hayato:[/white]「No notes to verify this bottle.」", "[white]隼人：[/white]「この瓶を照合する資料がない。」"]},
    "STONE_OF_ETERNITY": {
        "BENKEI_ROLE": ["[yellow]弁庆：[/yellow]需要铁壁或底力。", "[yellow]Benkei:[/yellow] Requires Wall or Prevail.", "[yellow]弁慶：[/yellow]鉄壁または底力が必要。"],
        "BENKEI_HP": ["[yellow]弁庆：[/yellow]需要大于12点当前生命。", "[yellow]Benkei:[/yellow] Requires more than 12 current HP.", "[yellow]弁慶：[/yellow]現在HPが12より多い必要がある。"]},
}
pilot_colors = {"RYOMA": "red", "HAYATO": "white", "BENKEI": "yellow"}
def colored_title(route, title):
    color = pilot_colors.get(route.split("_")[0])
    return f"[{color}]{title}[/{color}]" if color and f"[{color}]" not in title else title
for index, language in enumerate(source["languages"]):
    for table in ("cards", "relics", "potions", "events"):
        path = ROOT / f"ShinGetterMod/localization/{language}/{table}.json"
        current = json.loads(path.read_text(encoding="utf-8-sig"))
        additions = {key: values[index] for key, values in source["content"][table].items()}
        if table == "events":
            for event, locks in extra_locked.items():
                for name, values in locks.items():
                    prefix = f"SHIN_GETTER_EVENT_INVASION.{event}.pages.INITIAL.options.{name}_LOCKED"
                    additions[prefix + ".description"] = values[index]
                    driver = name.split("_")[0]
                    titles = {"RYOMA": ["龙马", "Ryoma", "竜馬"], "HAYATO": ["隼人", "Hayato", "隼人"], "BENKEI": ["弁庆", "Benkei", "弁慶"]}
                    additions[prefix + ".title"] = colored_title(driver, titles[driver][index])
            for route in source["routes"]:
                prefix = f"SHIN_GETTER_EVENT_INVASION.{route['event']}.pages."
                name = route["route"]
                dialogue, rules = route["description"][index].split("\n", 1)
                separator = ": " if language == "eng" else "："
                title = colored_title(name, route["title"][index]) + separator + dialogue
                additions[prefix + f"INITIAL.options.{name}.title"] = title
                additions[prefix + f"INITIAL.options.{name}.description"] = rules
                additions[prefix + f"INITIAL.options.{name}_LOCKED.title"] = title
                additions[prefix + f"INITIAL.options.{name}_LOCKED.description"] = route["locked"][index]
                additions[prefix + f"{name}_RESULT.description"] = route["result"][index]
                if "prompt" in route:
                    additions[prefix + f"{name}.selectionPrompt"] = route["prompt"][index]
        if args.check:
            bad = [key for key, value in additions.items() if current.get(key) != value]
            if bad:
                raise SystemExit(f"{language}/{table}: {len(bad)} mismatch: {bad[:3]}")
        else:
            current.update(additions)
            path.write_text(json.dumps(current, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("issue#238 authored localization merge/check PASS")
