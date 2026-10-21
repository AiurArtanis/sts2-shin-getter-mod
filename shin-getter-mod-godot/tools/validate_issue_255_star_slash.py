"""Runtime schema/provenance and cache contracts, with real data mutants; no engine launch."""
from __future__ import annotations
import copy
import hashlib
import json
import subprocess

from star_slash_runtime_metadata import BASE, BASE_HASHES, LAYERS, ROOT, encoded, expand_document, legacy_bytes, pack_document
from validate_issue_124 import data_contract, sequence_contract


def main():
    sequence = (ROOT / "src/Nodes/Combat/NShinGetterStarSlashSequence.cs").read_text(encoding="utf-8")
    data = (ROOT / "src/Nodes/Combat/NShinGetterStarSlashData.cs").read_text(encoding="utf-8")
    sequence_contract(sequence)
    data_contract(data)
    old_sequence = subprocess.check_output(["git", "show",
        f"{BASE}:shin-getter-mod-godot/src/Nodes/Combat/NShinGetterStarSlashSequence.cs"], cwd=ROOT.parent).decode()
    def union_methods(source):
        return source.split("private static Vector2[][] MergeOutline(", 1)[1].split("private Vector2 Local(", 1)[0].replace("\r\n", "\n")
    assert union_methods(sequence) == union_methods(old_sequence), "Native union algorithms/order changed"
    # The cleaner, UV formula, phase/selection/voice and card code also remain unchanged.
    for relative in ("src/Models/Cards/SGC_StarSlash.cs", "src/Audio/ShinGetterVoiceService.cs",
                     "src/Nodes/Combat/NShinGetterSpriteSequence.cs"):
        old = subprocess.check_output(["git", "show", f"{BASE}:shin-getter-mod-godot/{relative}"], cwd=ROOT.parent)
        assert (ROOT / relative).read_bytes() == old, "Unrelated gameplay/timing source changed"
    negatives = 0
    for action in BASE_HASHES:
        original = json.loads(legacy_bytes(action))
        delivery = json.loads((ROOT.parent / f"art_sources/characters/shin_getter/forms/{action}/delivery.json").read_text(encoding="utf-8"))
        path = ROOT / f"images/characters/shin_getter/forms/{action}/animation.json"
        current = json.loads(path.read_text(encoding="utf-8"))
        assert encoded(pack_document(original, delivery)) == path.read_text(encoding="utf-8"), "Generator output differs"
        assert expand_document(current, delivery) == original, "Exact inverse changed anchors/contours/provenance/times"
        assert all(key not in current["frames"][0] for key in ("index", "source_frame", "source_pts", "stage", "sha256"))
        def changed_pool(doc):
            doc["polygon_pool"][0][0][0] += .25
        def changed_reference(doc):
            doc["frames"][0]["weapon_cover"][0] = len(doc["polygon_pool"])
        def changed_weight(doc):
            doc["frames"][0]["duration_seconds"] *= 2
        def removed_frame(doc):
            doc["frames"].pop()
        def bad_provenance(doc):
            doc["delivery_sha256"] = "0" * 64
        def removed_hand(doc):
            doc["frames"][0]["hands"] = []
        def unsupported(doc):
            doc["schema_version"] = 1
        def permuted_cover(doc):
            doc["frames"][0]["weapon_cover"].reverse()
        for mutation in (changed_pool, changed_reference, changed_weight, removed_frame,
                         bad_provenance, removed_hand, unsupported, permuted_cover):
            broken = copy.deepcopy(current)
            mutation(broken)
            assert broken != current, "Mutation had no effect"
            try:
                assert expand_document(broken, delivery) == original
            except AssertionError:
                negatives += 1
                continue
            raise AssertionError("Runtime schema/data mutant escaped")
    print(f"issue#255 Star Slash PASS: exact inverse, preserved native union/gameplay; {negatives} schema/data mutants rejected.")
    print("Production-linked managed geometry/draw instrumentation and real GPU acceptance are separate.")


if __name__ == "__main__":
    main()
