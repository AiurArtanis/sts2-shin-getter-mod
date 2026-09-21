#!/usr/bin/env python3
"""Focused issue#214 source/asset contracts. No game, network or user config writes.

Not a substitute for runtime playback/focus evidence. Run only after review.
--combined193 additionally requires the reviewed issue#193 integration.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REMOVED = {"grief", "morning_on_the_tundra", "brutality", "memory",
           "interference", "cold_bloodedness", "resolve", "heats_final"}


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--combined193", action="store_true")
    args = parser.parse_args()
    manifest = json.loads(read("tools/issue214-bgm-manifest.json"))
    tracks = manifest["tracks"]
    assert manifest["sheet"] == "BGM列表" and len(tracks) == 23
    assert [t["row"] for t in tracks] == list(range(3, 26))
    catalog = read("src/Audio/ShinGetterBgmCatalog.cs")
    constants = dict(re.findall(r'const string (\w+) = "([^"]*)";', catalog))
    entries = []
    for identifier, path, suffix in re.findall(
            r'Track\(([^,]+),\s*(.*?),\s*"([A-Z0-9_]+)"', catalog):
        identifier = identifier.strip()
        identifier = identifier.strip('"') if identifier.startswith('"') else constants[identifier]
        resource = path.strip().removeprefix('$').strip('"')
        for key, value in constants.items():
            resource = resource.replace("{" + key + "}", value)
        entries.append((identifier, resource, suffix))
    assert [e[0] for e in entries] == ["default"] + [t["id"] for t in tracks] + ["random"]
    assert REMOVED.isdisjoint(e[0] for e in entries)
    assert "Random.Shared.Next(1, Tracks.Count - 1)" in catalog
    tables = {lang: json.loads(read(f"ShinGetterMod/localization/{lang}/settings_ui.json"))
              for lang in ("zhs", "jpn", "eng")}
    expected_paths = {t["resource"] for t in tracks}
    actual_paths = {p.relative_to(ROOT).as_posix()
                    for p in (ROOT / "audio/music/shin_getter").rglob("*.mp3")}
    assert actual_paths == expected_paths, actual_paths ^ expected_paths
    resource_gate = read("tools/validate-mod-resources.gd")
    for track, entry in zip(tracks, entries[1:-1]):
        assert entry[1] == "res://" + track["resource"], entry
        assert entry[2] == track["id"].upper()
        data = (ROOT / track["resource"]).read_bytes()
        assert len(data) == track["bytes"], track["id"]
        assert hashlib.sha256(data).hexdigest() == track["sha256"], track["id"]
        assert '"res://' + track["resource"] + '": false' in resource_gate
        for lang, table in tables.items():
            assert table["SHIN_GETTER_CHUNIBYO.BGM.TRACK." + entry[2]] == track["titles"][lang]
    for table in tables.values():
        keys = {k.rsplit(".", 1)[-1] for k in table if k.startswith("SHIN_GETTER_CHUNIBYO.BGM.TRACK.")}
        assert keys == {e[2] for e in entries}
    assert '"res://audio/music/shin_getter/album/heroic.mp3"' in catalog
    assert not (ROOT / "audio/music/shin_getter/execution_theme.mp3.import").exists()
    encounter = read("src/Audio/ShinGetterEncounterMusicService.cs")
    assert '(_, ShinGetterBgmCategory.EventCombat) => GetTrackPath("onslaught")' in encounter
    assert "(Hive, ShinGetterBgmCategory.EliteCombat) => HiveElite" in encounter
    assert 'encounters/elite_hive.mp3"' in encounter
    assert "LocalContext.GetMe(runState)?.Character is ShinGetter" in encounter
    assert "runState.Players.Any" not in encounter
    assert "MapPointType.Ancient" in encounter
    execution = read("src/Audio/ShinGetterExecutionMusicService.cs")
    assert "? ShinGetterBgmCatalog.DefaultExecutionMusicPath" in execution
    config = read("src/Config/ShinGetterChunibyoConfigService.cs")
    assert config.index("JsonSerializer.Deserialize") < config.index("NormalizeBgmSelections(Current)")
    normalize = config.split("internal static void NormalizeBgmSelections", 1)[1].split("public static bool Save", 1)[0]
    for category in ("Execution", "NormalCombat", "EventCombat", "EliteCombat", "BossCombat"):
        field = category + "BgmTrackId"
        assert f"config.{field} = ShinGetterBgmCatalog.ResolveOrDefault(config.{field}).Id;" in normalize
    assert "Save(" not in normalize and "VoiceMode" not in normalize and "BgmEnabled" not in normalize
    assert "StringComparison.OrdinalIgnoreCase" in catalog and "return Tracks[0];" in catalog
    if args.combined193:
        import validate_issue_193
        validate_issue_193.main()
        for text in (encounter, execution, read("src/Audio/ShinGetterBgmPreviewService.cs")):
            assert "!ShinGetterChunibyoConfigService.IsBgmEnabled" in text
    print("issue#214 focused source/asset contracts PASS; runtime playback not asserted")


if __name__ == "__main__":
    main()
