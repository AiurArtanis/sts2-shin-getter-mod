"""Capture current authoritative prose / mechanically merge authored translations.

The capture is developer-only and reads the vault; normal --check is self-contained.
No event mechanics, cost strings, or option title keys are rewritten by this tool.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools/issue238-legacy-localization.json"
REGISTRY = {
    "被寄生的自动机械": ("INFESTED_AUTOMATON", ["HAYATO"]),
    "多尼斯异鸟巢": ("BYRDONIS_NEST", ["BENKEI", "RYOMA", "RYOMA_HATCH"]),
    "光与暗的门扉": ("DOORS_OF_LIGHT_AND_DARK", ["RYOMA"]),
    "滑脚木桥": ("SLIPPERY_BRIDGE", ["RYOMA", "HAYATO"]),
    "镜中倒影 影倒中镜": ("REFLECTIONS", ["TRIPLE_UNITY"]),
    "巨大花卉": ("COLOSSAL_FLOWER", ["HAYATO", "RYOMA"]),
    "冷光合唱团": ("LUMINOUS_CHOIR", ["RYOMA", "HAYATO"]),
    "灵魂嫁接者": ("SPIRIT_GRAFTER", ["TRIPLE_UNITY"]),
    "螺旋漩涡": ("SPIRALING_WHIRLPOOL", ["HAYATO"]),
    "迷失鬼火": ("LOST_WISP", ["BENKEI", "HAYATO"]),
    "木雕": ("WOOD_CARVINGS", ["TRIPLE_CARVING"]),
    "泉水": ("WELLSPRING", ["RYOMA"]),
    "熔合者": ("AMALGAMATOR", ["RYOMA", "BENKEI"]),
    "深渊浴场": ("ABYSSAL_BATHS", ["TRIPLE_COOLANT"]),
    "审判": ("TRIAL", ["RYOMA"]),
    "水漫缮写室": ("WATERLOGGED_SCRIPTORIUM", ["HAYATO_ADAPTATION", "HAYATO_INK"]),
    "无尽传送带": ("ENDLESS_CONVEYOR", ["HAYATO"]),
    "无休之处": ("UNREST_SITE", ["RYOMA", "BENKEI_BREATH"]),
    "淹水灯塔": ("DROWNING_BEACON", ["RYOMA", "BENKEI_PRISM"]),
    "药水的未来？": ("THE_FUTURE_OF_POTIONS", ["HAYATO"]),
    "遗物交换商": ("RELIC_TRADER", ["HAYATO"]),
    "圆桌茶会": ("ROUND_TEA_PARTY", ["RYOMA"]),
    "长者兰伟德": ("RANWID_THE_ELDER", ["RYOMA", "RYOMA_RESULT"]),
    "茶艺大师": ("TEA_MASTER", ["RYOMA", "BENKEI"]),
    "沉没雕像": ("SUNKEN_STATUE", ["BENKEI"]),
    "打造时间": ("TINKER_TIME", ["HAYATO"]),
    "害虫杀手": ("BUGSLAYER", ["BENKEI"]),
    "欢迎来到旺购百货": ("WELCOME_TO_WONGOS", ["HAYATO"]),
    "垃圾堆": ("TRASH_HEAP", ["BENKEI"]),
    "满屋芝士": ("ROOM_FULL_OF_CHEESE", ["BENKEI"]),
    "这个还是那个？": ("THIS_OR_THAT", ["RYOMA", "HAYATO"]),
    "传说是真的": ("THE_LEGENDS_WERE_TRUE", ["RYOMA", "HAYATO"]),
}


def clean(text):
    text = re.sub(r"\[\[([^]|]+)(?:\|([^]]+))?\]\]", lambda m: m[2] or m[1], text)
    text = text.replace("`", "").replace("“", "「").replace("”", "」")
    return "\n\n".join(p.strip() for p in re.split(r"\n\s*\n", text.strip()))


def current_scene(text):
    if "## 完整场景" in text:
        return text.split("## 完整场景", 1)[1].split("## 历史设计与反馈", 1)[0]
    if "## 最终设计 2026-09-20" in text:
        return text.split("## 最终设计 2026-09-20", 1)[1]
    return text.split("## 修改意见", 1)[0].split("## 文本优化记录", 1)[0]


parser = argparse.ArgumentParser()
parser.add_argument("--capture-vault", type=Path)
parser.add_argument("--seed-identical", action="store_true")
parser.add_argument("--check", action="store_true")
args = parser.parse_args()
if args.capture_vault:
    previous = json.loads(SOURCE.read_text(encoding="utf-8")) if SOURCE.exists() else {"entries": []}
    old = {e["key"]: e for e in previous["entries"]}
    entries = []
    for name, (event, pages) in REGISTRY.items():
        path = f"游戏/杀戮尖塔2/事件笔记/事件：{name}.md"
        scene = current_scene((args.capture_vault / path).read_text(encoding="utf-8-sig").replace("\r\n", "\n"))
        pattern = r"^- \*\*(?:选择后(?:剧情|文本)(?:文本)?|胜利后返回事件剧情)\*\*：\s*\n(.*?)(?=^\s*- \*\*|^##|\Z)"
        paragraphs = [clean(m) for m in re.findall(pattern, scene, re.M | re.S)]
        if name == "长者兰伟德":
            body, tail = paragraphs[0].rsplit("\n\n", 1)
            paragraphs = [body, tail]
        if len(paragraphs) != len(pages):
            raise SystemExit(f"{name}: {len(paragraphs)} prose segments != {len(pages)} mapped pages")
        for page, zhs in zip(pages, paragraphs):
            key = f"SHIN_GETTER_EVENT_INVASION.{event}.pages.{page}.description"
            entry = {"key": key, "source": path, "chapter": "完整场景" if "当前唯一生效设计" in scene else "事件分支",
                     "scene_sha256": hashlib.sha256(scene.encode()).hexdigest(), "zhs": zhs}
            for lang in ("eng", "jpn"):
                if key in old and old[key]["zhs"] == zhs and lang in old[key]:
                    entry[lang] = old[key][lang]
            entries.append(entry)
    SOURCE.write_text(json.dumps({"entries": entries}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"captured {len(REGISTRY)} current notes / {len(entries)} prose pages; translations require authoring")
elif args.seed_identical:
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    current = {lang: json.loads((ROOT / f"ShinGetterMod/localization/{lang}/events.json").read_text(encoding="utf-8-sig"))
               for lang in ("zhs", "eng", "jpn")}
    for entry in source["entries"]:
        if clean(current["zhs"].get(entry["key"], "")) == entry["zhs"]:
            for lang in ("eng", "jpn"):
                if entry["key"] in current[lang]:
                    entry[lang] = clean(current[lang][entry["key"]])
    SOURCE.write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("reused only prose with identical current Chinese semantics")
else:
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    authored = json.loads((ROOT / "tools/issue238-legacy-translations.json").read_text(encoding="utf-8"))
    for lang in ("zhs", "eng", "jpn"):
        path = ROOT / f"ShinGetterMod/localization/{lang}/events.json"
        current = json.loads(path.read_text(encoding="utf-8-sig"))
        for entry in source["entries"]:
            short = entry["key"].removeprefix("SHIN_GETTER_EVENT_INVASION.").replace(".pages.", ".").removesuffix(".description")
            if short in authored and lang in ("eng", "jpn"):
                entry[lang] = authored[short][0 if lang == "eng" else 1]
            if not entry.get(lang):
                raise SystemExit(f"missing authored {lang}: {entry['key']}")
            if args.check:
                if current.get(entry["key"]) != entry[lang]:
                    raise SystemExit(f"mismatch {lang}: {entry['key']}")
            else:
                current[entry["key"]] = entry[lang]
        if not args.check:
            path.write_text(json.dumps(current, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"issue#238 legacy prose: {len(REGISTRY)} notes / {len(source['entries'])} pages / three languages PASS")
