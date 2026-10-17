"""Static startup wiring gates; these do not run Harmony, BS2, or the game."""
import argparse
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_PATCH = ROOT / "shin-getter-mod-godot/src/Patches/ShinGetterBs2MandalaPatch.cs"


def method_body(text, name):
    declaration = re.search(
        rf"\b(?:public|private|internal)\s+(?:(?:async|static)\s+)*[\w?<>]+\s+{name}\s*\([^;{{]*\)\s*{{",
        text,
        re.IGNORECASE,
    )
    if declaration is None:
        raise AssertionError(f"Method not found: {name}")
    start = declaration.end()
    depth = 1
    for index in range(start, len(text)):
        depth += (text[index] == "{") - (text[index] == "}")
        if depth == 0:
            return text[start:index]
    raise AssertionError(f"Unclosed method: {name}")


class StartupWiring(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.patch = ARGS.patch_source.read_text(encoding="utf-8-sig")
        cls.games = [(path, path.read_text(encoding="utf-8-sig")) for path in ARGS.run_manager]
        target = re.search(
            r'\[HarmonyPatch\(typeof\(RunManager\),\s*(?:"(\w+)"|nameof\(RunManager\.(\w+)\))\)\]',
            cls.patch,
        )
        if target is None:
            raise AssertionError("RunManager installation hook not found")
        cls.hook = target.group(1) or target.group(2)

    def assert_entry_installs(self, entry, *, saved=False):
        for path, game in self.games:
            with self.subTest(game=str(path), entry=entry):
                # Each case starts at its own setup method. No prior new game or
                # SetActInternal call is assumed, so warm-process setup cannot mask a gap.
                body = method_body(game, entry)
                call = re.search(rf"\b{self.hook}\s*\(", body)
                self.assertIsNotNone(call, f"{entry} does not reach installation hook {self.hook}")
                if saved:
                    restore = re.search(r"\bInitializeSavedRun\s*\(", body)
                    self.assertIsNotNone(restore)
                    self.assertLess(call.start(), restore.start(), "Install before restoring saved rooms")

    def test_new_singleplayer(self):
        self.assert_entry_installs("SetUpNewSinglePlayer")

    def test_new_multiplayer(self):
        self.assert_entry_installs("SetUpNewMultiPlayer")

    def test_cold_singleplayer_save_without_prior_new_run(self):
        self.assert_entry_installs("SetUpSavedSinglePlayer", saved=True)

    def test_cold_multiplayer_save_without_prior_new_run(self):
        self.assert_entry_installs("SetUpSavedMultiPlayer", saved=True)

    def test_postfix_invokes_optional_installer(self):
        self.assertIn("ShinGetterBs2MandalaCompatibility.TryInstall();", method_body(self.patch, "Postfix"))
        for path, game in self.games:
            with self.subTest(game=str(path)):
                method_body(game, self.hook)

    def test_install_once_and_skip_missing_bs2(self):
        installer = method_body(self.patch, "TryInstall")
        guard = re.search(r"if\s*\(_installationAttempted\)\s*return;", installer)
        self.assertIsNotNone(guard)
        self.assertLess(guard.start(), installer.index("_installationAttempted = true;"))
        self.assertLess(installer.index("_installationAttempted = true;"), installer.index("GetAssemblies()"))
        absent = re.search(r"if\s*\(bs2 == null\)\s*return;", installer)
        self.assertIsNotNone(absent)
        self.assertLess(absent.start(), installer.index("harmony.Patch("))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-manager", type=Path, action="append", required=True,
                        help="Read-only RunManager.cs; repeat for each target game version")
    parser.add_argument("--patch-source", type=Path, default=DEFAULT_PATCH,
                        help="Override only for the rejected-baseline negative control")
    ARGS, unittest_args = parser.parse_known_args()
    unittest.main(argv=[__file__, *unittest_args])
