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
    )

    expected_localization = {
        "zhs": {
            "SHIN_GETTER.voice.hayatoNoHpLoss": "哼，别逗我笑了！",
            "SHIN_GETTER.voice.benkeiNoHpLoss": "没办法，我来当盾牌。",
        },
        "eng": {
            "SHIN_GETTER.voice.hayatoNoHpLoss": "Hmph, don't make me laugh!",
            "SHIN_GETTER.voice.benkeiNoHpLoss": "Can't be helped. I'll be the shield.",
        },
        "jpn": {
            "SHIN_GETTER.voice.hayatoNoHpLoss": "フン、笑わせるな！",
            "SHIN_GETTER.voice.benkeiNoHpLoss": "仕方ねえ、俺が盾になる。",
        },
    }
    for locale, expected in expected_localization.items():
        path = ROOT / "ShinGetterMod/localization" / locale / "characters.json"
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        for key, value in expected.items():
            if data.get(key) != value:
                raise AssertionError(f"{path}: expected {key}={value!r}")

    for filename in ("hayato_no_hp_loss.wav", "benkei_no_hp_loss.wav"):
        audio = VOICE_DIR / filename
        import_file = VOICE_DIR / f"{filename}.import"
        if not audio.is_file() or audio.stat().st_size == 0:
            raise AssertionError(f"Missing voice audio: {audio}")
        if not import_file.is_file():
            raise AssertionError(f"Missing Godot import descriptor: {import_file}")

    print("issue#169 static validation passed")


if __name__ == "__main__":
    main()
