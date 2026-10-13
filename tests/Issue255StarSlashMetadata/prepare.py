"""Create excluded regression inputs from the exact Git baseline, not a hand-built model."""
import hashlib
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "shin-getter-mod-godot/tools"))
from star_slash_runtime_metadata import BASE, BASE_HASHES, legacy_bytes

output = Path(__file__).parent / "obj"
output.mkdir(exist_ok=True)
relative = "shin-getter-mod-godot/src/Nodes/Combat/NShinGetterStarSlashData.cs"
source = subprocess.check_output(["git", "show", f"{BASE}:{relative}"], cwd=ROOT)
# Only rename the class to coexist with the production-linked implementation.
(output / "LegacyData.cs").write_text(source.decode().replace("NShinGetterStarSlashData", "LegacyStarSlashData"), encoding="utf-8")
sequence = subprocess.check_output(["git", "show",
    f"{BASE}:shin-getter-mod-godot/src/Nodes/Combat/NShinGetterStarSlashSequence.cs"], cwd=ROOT)
(output / "LegacySequence.cs").write_text(sequence.decode().replace("NShinGetterStarSlashSequence", "LegacyStarSlashSequence")
    .replace("NShinGetterStarSlashData", "LegacyStarSlashData"), encoding="utf-8")
for action in BASE_HASHES:
    (output / (action + ".json")).write_bytes(legacy_bytes(action))
print("Exact Git baseline fixture inputs prepared; source SHA-256=" + hashlib.sha256(source).hexdigest())
