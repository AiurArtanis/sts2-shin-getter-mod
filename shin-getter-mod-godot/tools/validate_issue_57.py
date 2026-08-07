from __future__ import annotations

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / "audio" / "music" / "shin_getter" / "encounters"

TRACKS = {
    "elite_overgrowth.mp3": (3046268, "00a57d27e3ce439d2f72527ab9ccd5208ba2bbbf0a7c2ab748887afc9e0ebfef"),
    "elite_underdocks.mp3": (2992772, "307b8d828c872bf55ab6c79e2f8262f1005e8665014f686114958770a3a8bc34"),
    "elite_hive.mp3": (3394010, "2ea32dc98c694043d9d6bd0af9709fce75ac9645c14bacef7a15c14632a57e63"),
    "elite_glory.mp3": (3914370, "c904fc1a900b0e6a49cb0f20a1dd0575cc6a053e2cca17b8aabd966e23c41385"),
    "boss_overgrowth.mp3": (3532359, "747fe5dfb9878e5b065e412fff24fc043f054c3406b5b5bddeb1da0d6794ef23"),
    "boss_underdocks.mp3": (3465481, "d0ac4cb0c2b1b5320df10ef0e80dcccc8116b42ffbef05651c6a918d3d7d2cd8"),
    "boss_hive.mp3": (1550155, "8866717f4c52ecb9878e1101bea1ed4f15df3655d59e80104c5909b3e0ad2449"),
    "boss_glory.mp3": (2833947, "9f607de8cbf0419dba013b42f265eff8ee32544ce80aa78fe5040271c3fed574"),
}

TITLES = {
    "zhs": {
        "BGM_RELIEF": "安堵", "BGM_GRIEF": "悲痛", "BGM_MORNING_ON_TUNDRA": "冰原之晨",
        "BGM_BRUTALITY": "残虐", "BGM_REBEL_ARMY": "反叛军", "BGM_PAST": "过去",
        "BGM_MEMORY": "记忆", "BGM_INTERFERENCE": "扰乱", "BGM_TENSION": "紧迫",
        "BGM_COLD_BLOODEDNESS": "冷酷", "BGM_MYSTERY": "谜", "BGM_MOMENTUM": "气势",
        "BGM_MAJESTY": "威风", "BGM_UNKNOWN": "未知", "BGM_ONSLAUGHT": "来袭",
        "BGM_BOND_OF_BLOOD": "血之羁绊", "BGM_RESOLVE": "一念", "BGM_HEROIC": "勇壮",
        "BGM_HYMN": "赞歌", "BGM_REMINISCENCE": "追忆", "BGM_FINAL_WAR": "总决战",
        "BGM_HURRY_UP_DREAM_DEPARTURE": "加油向前的梦想~出发~",
        "BGM_HURRY_UP_DREAM_DEPARTURE_OVA": "加油向前的梦想~出发~（OVA版）",
        "BGM_HEATS": "HEATS", "BGM_HEATS_OVA": "HEATS（OVA版）",
    },
    "eng": {
        "BGM_RELIEF": "Relief", "BGM_GRIEF": "Grief", "BGM_MORNING_ON_TUNDRA": "Morning on the Tundra",
        "BGM_BRUTALITY": "Brutality", "BGM_REBEL_ARMY": "Rebel Army", "BGM_PAST": "Past",
        "BGM_MEMORY": "Memory", "BGM_INTERFERENCE": "Interference", "BGM_TENSION": "Tension",
        "BGM_COLD_BLOODEDNESS": "Cold-Bloodedness", "BGM_MYSTERY": "Mystery", "BGM_MOMENTUM": "Momentum",
        "BGM_MAJESTY": "Majesty", "BGM_UNKNOWN": "Unknown", "BGM_ONSLAUGHT": "Onslaught",
        "BGM_BOND_OF_BLOOD": "Bond of Blood", "BGM_RESOLVE": "Resolve", "BGM_HEROIC": "Heroic",
        "BGM_HYMN": "Hymn", "BGM_REMINISCENCE": "Reminiscence", "BGM_FINAL_WAR": "Final War",
        "BGM_HURRY_UP_DREAM_DEPARTURE": "HURRY UP DREAM ~ Departure ~",
        "BGM_HURRY_UP_DREAM_DEPARTURE_OVA": "HURRY UP DREAM ~ Departure ~ (OVA Version)",
        "BGM_HEATS": "HEATS", "BGM_HEATS_OVA": "HEATS (OVA Version)",
    },
    "jpn": {
        "BGM_RELIEF": "安堵", "BGM_GRIEF": "悲痛", "BGM_MORNING_ON_TUNDRA": "氷原の朝",
        "BGM_BRUTALITY": "残虐", "BGM_REBEL_ARMY": "反乱軍", "BGM_PAST": "過去",
        "BGM_MEMORY": "記憶", "BGM_INTERFERENCE": "撹乱", "BGM_TENSION": "緊迫",
        "BGM_COLD_BLOODEDNESS": "冷酷", "BGM_MYSTERY": "謎", "BGM_MOMENTUM": "気勢",
        "BGM_MAJESTY": "威風", "BGM_UNKNOWN": "未知", "BGM_ONSLAUGHT": "襲来",
        "BGM_BOND_OF_BLOOD": "血の絆", "BGM_RESOLVE": "一念", "BGM_HEROIC": "勇壮",
        "BGM_HYMN": "賛歌", "BGM_REMINISCENCE": "追憶", "BGM_FINAL_WAR": "総大戦",
        "BGM_HURRY_UP_DREAM_DEPARTURE": "HURRY UP DREAM~旅立ち~",
        "BGM_HURRY_UP_DREAM_DEPARTURE_OVA": "HURRY UP DREAM~旅立ち~（OVA Version）",
        "BGM_HEATS": "HEATS", "BGM_HEATS_OVA": "HEATS (OVA Version)",
    },
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


for name, (size, digest) in TRACKS.items():
    path = AUDIO / name
    require(path.is_file(), f"missing mapped BGM: {name}")
    require(path.stat().st_size == size, f"unexpected size for {name}")
    require(hashlib.sha256(path.read_bytes()).hexdigest() == digest, f"unexpected SHA-256 for {name}")

service = (ROOT / "src" / "Audio" / "ShinGetterEncounterMusicService.cs").read_text(encoding="utf-8")
catalog = (ROOT / "src" / "Audio" / "ShinGetterBgmCatalog.cs").read_text(encoding="utf-8")
for act in ("Overgrowth", "Underdocks", "Hive", "Glory"):
    for room_type in ("Elite", "Boss"):
        require(f"({act}, RoomType.{room_type})" in service, f"missing {act} {room_type} mapping")
require(
    "LocalContext.GetMe(runState)?.Character is ShinGetter" in service,
    "music replacement must be limited to the local Shin Getter player",
)
require(
    "runState.CurrentMapPointHistoryEntry?.MapPointType == MapPointType.Ancient" in service,
    "Ancient map points must keep their original music",
)
require(
    "runState.Players.Any" not in service,
    "a remote Shin Getter player must not change this client's music",
)
require(
    "ShinGetterBgmCatalog.GetRelativeVolume(category)" in service,
    "custom encounter BGM must use the category-relative volume",
)
require(
    "category == ShinGetterBgmCategory.Execution ? 1f : 0.70f" in catalog,
    "non-execution custom BGM must play at 70% of the configured BGM volume",
)
require("StopActiveAndRestore" in service and "SuspendForExecution" in service, "music lifecycle guards are missing")

execution = (ROOT / "src" / "Audio" / "ShinGetterExecutionMusicService.cs").read_text(encoding="utf-8")
require("ShinGetterEncounterMusicService.SuspendForExecution()" in execution, "execution music must replace encounter music")
require("StopImmediatelyAndRestore" in execution, "new combats must cancel a pending execution-music fade")
require(
    "owner.PlayerCombatState is not { TurnNumber: >= 2 }" in execution,
    "execution music must not replace encounter music during the first player turn",
)

lifecycle = (ROOT / "src" / "Patches" / "ShinGetterCombatLifecyclePatch.cs").read_text(encoding="utf-8")
require("ShinGetterEncounterMusicService.StopActiveAndRestore()" in lifecycle, "CombatManager.Reset must restore original BGM")

for language, expected in TITLES.items():
    path = ROOT / "ShinGetterMod" / "localization" / language / "music.json"
    actual = json.loads(path.read_text(encoding="utf-8"))
    require(actual == expected, f"{language}/music.json does not match the 25-row BGM workbook")

print("issue#57 static validation PASS: 8 mapped tracks, 25 trilingual titles, lifecycle guards")
