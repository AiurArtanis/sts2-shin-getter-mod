#!/usr/bin/env python3
"""Static regression gate for issue#169."""

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VOICE_DIR = ROOT / "audio/sfx/characters/shin_getter/voices"


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8-sig")


def require(relative_path: str, *fragments: str) -> None:
    text = read(relative_path)
    for fragment in fragments:
        if fragment not in text:
            raise AssertionError(f"{relative_path}: missing {fragment!r}")


def main() -> None:
    require(
        "src/Audio/ShinGetterVoiceService.cs",
        "HayatoNoHpLoss",
        "BenkeiNoHpLoss",
        'new("049", ShinGetterVoiceCue.HayatoNoHpLoss, "hayato_no_hp_loss.wav"',
        'new("050", ShinGetterVoiceCue.BenkeiNoHpLoss, "benkei_no_hp_loss.wav"',
        "dealer?.Side == CombatSide.Enemy",
        "props.HasFlag(ValueProp.Move)",
        "ShinGetterForm.Getter2 => ShinGetterVoiceCue.HayatoNoHpLoss",
        "ShinGetterForm.Getter3 => ShinGetterVoiceCue.BenkeiNoHpLoss",
        "RyomaLowHp50",
        "RyomaLowHp25",
        "HayatoLowHp50",
        "HayatoLowHp25",
        "BenkeiLowHp50",
        "BenkeiLowHp25",
        'new("052", ShinGetterVoiceCue.RyomaLowHp50, "ryoma_low_hp_50.wav"',
        'new("053", ShinGetterVoiceCue.RyomaLowHp25, "ryoma_low_hp_25.wav"',
        'new("054", ShinGetterVoiceCue.HayatoLowHp50, "hayato_low_hp_50.wav"',
        'new("055", ShinGetterVoiceCue.HayatoLowHp25, "hayato_low_hp_25.wav"',
        'new("056", ShinGetterVoiceCue.BenkeiLowHp50, "benkei_low_hp_50.wav"',
        'new("057", ShinGetterVoiceCue.BenkeiLowHp25, "benkei_low_hp_25.wav"',
        "OnAfterCurrentHpChanged",
        "TryHandleLowHpThresholdVoice(player, creature, delta, state)",
        "AreLowHpThresholdVoicesSuppressed(player)",
        "delta >= 0m",
        "previousHp * 4m >= target.MaxHp",
        "target.CurrentHp * 4m < target.MaxHp",
        "previousHp * 2m >= target.MaxHp",
        "target.CurrentHp * 2m < target.MaxHp",
        "state.HasHandledLowHp25 = true",
        "state.HasHandledLowHp50 = true",
        "GetLowHpVoiceCue(player, true)",
        "GetLowHpVoiceCue(player, false)",
    )

    expected_localization = {
        "zhs": {
            "SHIN_GETTER.voice.hayatoNoHpLoss": "哼，别逗我笑了！",
            "SHIN_GETTER.voice.benkeiNoHpLoss": "没办法，我来当盾牌。",
            "SHIN_GETTER.voice.ryomaLowHp50": "越是危险，我越是兴奋！",
            "SHIN_GETTER.voice.ryomaLowHp25": "只要身体还能动，我就不会认输",
            "SHIN_GETTER.voice.hayatoLowHp50": "切，现在还远没到认命的时候",
            "SHIN_GETTER.voice.hayatoLowHp25": "我和盖塔竟然被逼入绝境！？",
            "SHIN_GETTER.voice.benkeiLowHp50": "哼，离致命伤还远得很",
            "SHIN_GETTER.voice.benkeiLowHp25": "别开玩笑了，胜负才刚开始",
        },
        "eng": {
            "SHIN_GETTER.voice.hayatoNoHpLoss": "Hmph, don't make me laugh!",
            "SHIN_GETTER.voice.benkeiNoHpLoss": "Can't be helped. I'll be the shield.",
            "SHIN_GETTER.voice.ryomaLowHp50": "The more dangerous it gets, the more excited I am!",
            "SHIN_GETTER.voice.ryomaLowHp25": "As long as my body can still move, I won't give up.",
            "SHIN_GETTER.voice.hayatoLowHp50": "Tch, it's nowhere near time to accept defeat.",
            "SHIN_GETTER.voice.hayatoLowHp25": "Getter and I have actually been driven into a corner?!",
            "SHIN_GETTER.voice.benkeiLowHp50": "Hmph, I'm nowhere near a fatal wound.",
            "SHIN_GETTER.voice.benkeiLowHp25": "Don't joke around. The fight's only just begun.",
        },
        "jpn": {
            "SHIN_GETTER.voice.hayatoNoHpLoss": "フン、笑わせるな！",
            "SHIN_GETTER.voice.benkeiNoHpLoss": "仕方ねえ、俺が盾になる。",
            "SHIN_GETTER.voice.ryomaLowHp50": "危険であればあるほど、俺は燃えるぜ！",
            "SHIN_GETTER.voice.ryomaLowHp25": "体が動く限り、俺は負けねえ！",
            "SHIN_GETTER.voice.hayatoLowHp50": "チッ、まだ諦めるには早すぎる。",
            "SHIN_GETTER.voice.hayatoLowHp25": "俺とゲッターがここまで追い詰められるとは！？",
            "SHIN_GETTER.voice.benkeiLowHp50": "フン、致命傷にはほど遠いぜ。",
            "SHIN_GETTER.voice.benkeiLowHp25": "ふざけるな、勝負はこれからだ。",
        },
    }
    for locale, expected in expected_localization.items():
        path = ROOT / "ShinGetterMod/localization" / locale / "characters.json"
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        for key, value in expected.items():
            if data.get(key) != value:
                raise AssertionError(f"{path}: expected {key}={value!r}")

    for filename in (
        "hayato_no_hp_loss.wav",
        "benkei_no_hp_loss.wav",
        "ryoma_low_hp_50.wav",
        "ryoma_low_hp_25.wav",
        "hayato_low_hp_50.wav",
        "hayato_low_hp_25.wav",
        "benkei_low_hp_50.wav",
        "benkei_low_hp_25.wav",
    ):
        audio = VOICE_DIR / filename
        import_file = VOICE_DIR / f"{filename}.import"
        if not audio.is_file() or audio.stat().st_size == 0:
            raise AssertionError(f"Missing voice audio: {audio}")
        if not import_file.is_file():
            raise AssertionError(f"Missing Godot import descriptor: {import_file}")

    service = read("src/Audio/ShinGetterVoiceService.cs")
    reset = service.split("internal static void ResetCombatVoiceHistory", 1)[1].split(
        "internal static async Task PlayShiningSparkIntro", 1
    )[0]
    for fragment in (
        "state.HasHandledLowHp50 = false",
        "state.HasHandledLowHp25 = false",
    ):
        if fragment not in reset:
            raise AssertionError(f"Missing combat-reset assertion: {fragment}")

    damage_handler = service.split("internal static void OnAfterDamageReceived", 1)[1].split(
        "private static bool TryHandleLowHpThresholdVoice", 1
    )[0]
    if "TryHandleLowHpThresholdVoice" in damage_handler:
        raise AssertionError("Low-HP thresholds must be consumed by the HP-change hook only")

    for relic_path in (
        "src/Models/Relics/SGR_GetterFurnace.cs",
        "src/Models/Relics/SGR_EmperorsFragment.cs",
    ):
        require(
            relic_path,
            "public override Task AfterCurrentHpChanged(Creature creature, decimal delta)",
            "ShinGetterVoiceService.OnAfterCurrentHpChanged(Owner, creature, delta)",
        )

    print("issue#169 static validation passed")


if __name__ == "__main__":
    main()
