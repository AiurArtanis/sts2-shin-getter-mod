#!/usr/bin/env python3
"""Approved audio, localization and source contracts; no Godot/game execution."""

from __future__ import annotations

import hashlib
import json
import re
import subprocess
import unittest
import wave
from pathlib import Path

from validate_b140_balance import block, ordered, require

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parent
VOICE = "src/Audio/ShinGetterVoiceService.cs"
HAT = "src/Models/Relics/SGR_MusashiClone.cs"
BASELINE = "0382893a97319d5bf2c07e3d4e6b39e4dd2dd928"
MANIFEST = json.loads((ROOT / "tools/issue270-approved-audio.json").read_text(encoding="utf-8"))
ASSETS = MANIFEST["assets"]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8-sig")


def wiring(service: str, hat: str) -> None:
    play = block(service, "internal static void TryPlayMusashiHat(")
    require(play, "player.Character is not ShinGetter", "ShinGetterVoiceMode.Silent",
            "NonInteractiveMode.IsActive", "ignoreRequiredForm: true", "catch (Exception exception)",
            "GD.PushWarning", 'isShinDragon ? "075"',
            'forms.Contains(ShinGetterForm.Getter1) ? "072"',
            'forms.Contains(ShinGetterForm.Getter2) ? "073"',
            'forms.Contains(ShinGetterForm.Getter3) ? "074"')
    for forbidden in ("await ", "TryClaimVoiceCue", "TryPlayOneTime", "TryGetPlayedVoiceMasks"):
        assert forbidden not in play
    ordered(play, 'isShinDragon ? "075"', 'Getter1) ? "072"', 'Getter2) ? "073"', 'Getter3) ? "074"')
    for asset in ASSETS:
        require(service, f'new("{asset["code"]}", null, "{asset["file"]}", '
                f'"SHIN_GETTER.voice.{asset["key"]}", Category: VoicePlaybackCategory.FatalDamageResponse)')
    audio = block(service, "private static bool TryPlayVoiceAudio(")
    require(audio, "or VoicePlaybackCategory.FatalDamageResponse)",
            "if (category is VoicePlaybackCategory.DamageResponse or VoicePlaybackCategory.FatalDamageResponse)",
            "state.ActiveDamageResponsePlayer = audioPlayer;", "StopAllVoiceAudio(state);")
    ordinary = block(service, "private static bool TryPlayOneTime(Player player, VoiceLine line, out")
    require(ordinary, "line.Category == VoicePlaybackCategory.DamageResponse", "HasActiveDamageResponse(player)")
    after = block(hat, "public override async Task AfterModifyingHpLostAfterOsty()")
    ordered(after, "ShinGetterForm[] forms = _pendingForms;", "bool isShinDragon = _pendingShinDragon;",
            "_pendingForms = Array.Empty<ShinGetterForm>();", "_pendingShinDragon = false;",
            "if (forms.Length == 0)", "ShinGetterVoiceService.TryPlayMusashiHat(Owner, forms, isShinDragon);",
            "await PowerCmd.Apply<SGP_HotBlood>")
    assert hat.count("TryPlayMusashiHat(") == 1
    prevent = block(hat, "internal decimal PreventFatalHpLoss(")
    require(prevent, "_pendingShinDragon = target.HasPower<SGP_ShinForm>();")
    for signature in ("public override Task BeforeCombatStart()", "public override Task AfterCombatEnd("):
        require(block(hat, signature), "_pendingShinDragon = false;")


class HatVoiceTests(unittest.TestCase):
    def test_wiring(self):
        wiring(read(VOICE), read(HAT))

    def test_old_history_and_registry_unchanged(self):
        baseline = subprocess.check_output(["git", "show", f"{BASELINE}:shin-getter-mod-godot/{VOICE}"],
                                           cwd=REPO).decode("utf-8")
        current = read(VOICE)
        for signature in ("internal enum ShinGetterVoiceCue", "private readonly record struct VoiceHistoryMasks("):
            self.assertEqual(block(baseline, signature), block(current, signature))
        pattern = r'new\("(\d{3})",([^\n]+)'
        old_rows, new_rows = dict(re.findall(pattern, baseline)), dict(re.findall(pattern, current))
        self.assertEqual(len(new_rows), len(re.findall(pattern, current)), "duplicate voice code")
        self.assertEqual({code: new_rows[code] for code in old_rows}, old_rows)
        self.assertEqual(set(new_rows) - set(old_rows), {"072", "073", "074", "075"})

    def test_approved_binary_and_imports(self):
        self.assertEqual([a["version"] for a in ASSETS], ["V9", "V9", "V7", "V5"])
        for asset in ASSETS:
            with self.subTest(code=asset["code"]):
                path = ROOT / "audio/sfx/characters/shin_getter/voices" / asset["file"]
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), asset["sha256"])
                with wave.open(str(path)) as wav:
                    self.assertEqual((wav.getnchannels(), wav.getframerate(), wav.getsampwidth()), (1, 48000, 2))
                    self.assertAlmostEqual(wav.getnframes() / wav.getframerate(), asset["duration"], places=3)
                    self.assertEqual(len(wav.readframes(wav.getnframes())), wav.getnframes() * 2)
                sidecar = path.with_name(path.name + ".import").read_text(encoding="utf-8")
                res = "res://audio/sfx/characters/shin_getter/voices/" + asset["file"]
                md5 = hashlib.md5(res.encode()).hexdigest()
                require(sidecar, f'source_file="{res}"', f'{asset["file"]}-{md5}.sample',
                        "compress/mode=0", "edit/trim=false", "edit/normalize=false",
                        "force/max_rate=false", "force/mono=false", "force/8_bit=false", "edit/loop_mode=0")
                require(read("tools/validate-mod-resources.gd"), f'"{res}": false')

    def test_approved_subtitles_and_effects(self):
        for language in ("zhs", "jpn", "eng"):
            loc = json.loads(read(f"ShinGetterMod/localization/{language}/characters.json"))
            for asset in ASSETS:
                with self.subTest(language=language, code=asset["code"]):
                    text = loc[f'SHIN_GETTER.voice.{asset["key"]}']
                    self.assertTrue(text.startswith(f'[{asset["color"]}]'))
                    self.assertTrue(text.endswith(f'[/{asset["color"]}]'))
                    name = {"zhs": "武藏", "jpn": "ムサシ", "eng": "Musashi"}[language]
                    self.assertIn(f"[jitter]{name}[/jitter]", text)
                    plain = re.sub(r"\[[^\]]+\]", "", text)
                    if language != "eng":
                        self.assertEqual(plain, asset[language])
            self.assertNotIn("死了好多年", loc["SHIN_GETTER.voice.musashiHatTwo"])

    def test_console_codes(self):
        usage = "<001-047|049-050|052-067|072-075>"
        require(read(VOICE), "Usage: sgs " + usage)
        console = read("src/Diagnostics/ShinGetterChunibyoConsoleCmd.cs")
        require(console, f'Args => "{usage}"', "Usage: sgs " + usage)

    def test_source_mutations_rejected(self):
        service, hat = read(VOICE), read(HAT)
        mutants = (
            ("voice", "ShinGetterVoiceMode.Silent", "ShinGetterVoiceMode.Always"),
            ("voice", 'isShinDragon ? "075"', 'isShinDragon ? "072"'),
            ("voice", 'Getter2) ? "073"', 'Getter2) ? "074"'),
            ("voice", 'new("072", null,', 'new("072", ShinGetterVoiceCue.ChangeGetterOne,'),
            ("voice", "or VoicePlaybackCategory.FatalDamageResponse)", ")"),
            ("voice", "state.ActiveDamageResponsePlayer = audioPlayer;", "// no priority guard"),
            ("voice", "catch (Exception exception)", "catch (InvalidOperationException exception)"),
            ("hat", "bool isShinDragon = _pendingShinDragon;", "bool isShinDragon = false;"),
            ("hat", "_pendingShinDragon = false;", "// stale form"),
            ("hat", "if (forms.Length == 0)", "if (false)"),
            ("hat", "ShinGetterVoiceService.TryPlayMusashiHat(Owner, forms, isShinDragon);", "// no voice"),
        )
        for target, old, new in mutants:
            with self.subTest(target=target, mutation=old):
                source = service if target == "voice" else hat
                self.assertIn(old, source)
                with self.assertRaises((AssertionError, ValueError)):
                    wiring(service.replace(old, new) if target == "voice" else service,
                           hat.replace(old, new) if target == "hat" else hat)
        print(f"Rejected {len(mutants)} voice source mutations")


if __name__ == "__main__":
    unittest.main(verbosity=2)
