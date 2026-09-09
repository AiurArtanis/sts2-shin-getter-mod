"""Static naming gate; legacy literals are restricted to migration and test inputs."""
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "shin-getter-mod-godot"
LEGACY_INPUTS = {
    "shin-getter-mod-godot/src/Patches/ShinGetterSaintDragonMigrationPatch.cs",
    "tests/SaintDragonSaveMigration/Program.cs",
}
OLD = re.compile(r"holy[ _]?dragon", re.IGNORECASE)
paths = subprocess.check_output(
    ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=ROOT
).decode("utf-8").split("\0")
errors = []
for name in set(filter(None, paths)):
    path = ROOT / name
    if not path.is_file():
        continue
    if OLD.search(name):
        errors.append(f"legacy filename: {name}")
    data = path.read_bytes()
    if b"\0" in data or name in LEGACY_INPUTS:
        continue
    if OLD.search(data.decode("utf-8-sig", errors="replace")):
        errors.append(f"legacy content outside migration inputs: {name}")

for language in ("eng", "jpn", "zhs"):
    localization = PROJECT / "ShinGetterMod/localization" / language
    cards = json.loads((localization / "cards.json").read_text(encoding="utf-8-sig"))
    events = json.loads((localization / "events.json").read_text(encoding="utf-8-sig"))
    for suffix in ("title", "description"):
        assert f"S_G_C_SAINT_DRAGON_ROAR.{suffix}" in cards
        assert f"S_G_E_GETTER_MANDALA.pages.INITIAL.options.SAINT_DRAGON.{suffix}" in events
    assert "S_G_E_GETTER_MANDALA.pages.SAINT_DRAGON.description" in events
    if language == "eng":
        assert cards["S_G_C_SAINT_DRAGON_ROAR.title"] == "Saint Dragon Roar"
assert (PROJECT / "src/Models/Cards/SGC_SaintDragonRoar.cs").is_file()
pool = (PROJECT / "src/Models/CardPools/ShinGetterCardPool.cs").read_text(encoding="utf-8-sig")
entries = re.findall(r"ModelDb\.Card<([^>]+)>\(\)", pool)
assert len(entries) == 77 and len(set(entries)) == 77
assert entries.count("SGC_SaintDragonRoar") == 1
assert not errors, "\n".join(errors)
print("Saint Dragon naming: PASS (legacy values only in explicit migration/test inputs)")
