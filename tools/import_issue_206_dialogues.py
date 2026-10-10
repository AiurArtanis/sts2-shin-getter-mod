"""Mechanically import the approved numbered manuscript; never rewrites the source.

This is a content importer, not a test runner. Pass the manuscript directory explicitly.
Locale translations live separately and must use these stable dialogue IDs.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("manuscripts", type=Path)
    args = parser.parse_args()
    names = ["欧洛巴斯", "坦克斯", "瓦库", "达弗", "佩尔", "特兹卡塔拉", "诺奴佩普", "涅奥"]
    files = [args.manuscripts / f"先古之民：{name}.md" for name in names]
    files.append(args.manuscripts / "终局对峙：建筑师.md")
    catalogue = []
    for file in files:
        source = file.read_text(encoding="utf-8-sig")
        npc = re.search(r"^原版标识: (\w+)$", source, re.M)[1]
        for match in re.finditer(r"对话ID：`([A-Z0-9_]+)`。([\s\S]*?)(?=\n#{1,6} |\Z)", source):
            key, body = match.groups()
            lines = re.findall(r"^\d+\. (.+)$", body, re.M)
            if not lines:
                raise ValueError(f"No numbered lines: {key}")
            option = re.search(r"^显示选项：(.+)$", body, re.M)
            scene = re.search(r"^场景设定：(.+)$", body, re.M)
            bond = re.search(r"_(RYOMA|HAYATO|BENKEI)_BOND_(\d+)$", key)
            record = dict(Id=key, Npc=npc, Driver=bond[1] if bond else "",
                          Stage=int(bond[2]) if bond else 0,
                          Option=option[1] if option else "", Scene=scene[1] if scene else "",
                          Lines=lines)
            record["Version"] = hashlib.sha256(json.dumps(record, ensure_ascii=False, sort_keys=True).encode()).hexdigest()[:16]
            catalogue.append(record)
    output = Path(__file__).resolve().parents[1] / "shin-getter-mod-godot/data/dialogues/zhs.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(catalogue, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Imported {len(catalogue)} dialogues from the current manuscript; no tests executed.")


if __name__ == "__main__":
    main()
