"""Narrow source gates supplement the linked card tests, not native rendering."""
import unittest
from pathlib import Path

SRC = Path(__file__).resolve().parents[2] / "shin-getter-mod-godot/src"


def source(path):
    return (SRC / path).read_text(encoding="utf-8-sig")


class TimingContracts(unittest.TestCase):
    def test_expansion_wait_and_single_voice_owner(self):
        card = source("Models/Cards/SGC_ExpansionStrike.cs")
        self.assertIn("TryPlayCardVoiceAtCustomTiming(this, out float duration) && duration > 0f", card)
        self.assertLess(card.index("Math.Min(0.86f, duration)"), card.index("await PlayMovementVfx"))
        self.assertNotIn(".BeforeDamage(", card)
        self.assertIn(".WithNoAttackerAnim()", card)
        voice = source("Audio/ShinGetterVoiceService.cs")
        custom = voice.split("private static bool UsesCustomCardVoiceTiming", 1)[1].split("private static VoiceLine?", 1)[0]
        self.assertIn("SGC_ExpansionStrike", custom)
        base = source("Models/Cards/ShinGetterCardBase.cs")
        deferred = base.split("DeferredCardVoiceCards =", 1)[1].split("FormTransformCards =", 1)[0]
        self.assertIn('"SGC_ExpansionStrike"', deferred)

    def test_expansion_impact_and_cleanup(self):
        vfx = source("Nodes/Vfx/ShinGetterCombatVfx.cs").split("public static async Task PlayExpansionRush", 1)[1].split("public static async Task PlayAvalanche", 1)[0]
        self.assertIn("await impact();\n            return;", vfx)
        visible = vfx.split("Vector2 originalScale", 1)[1]
        self.assertLess(visible.index("await impact();"), visible.index('"global_position", origin, 0.16f'))
        self.assertIn("finally", visible)
        self.assertIn("ownerNode.GlobalPosition = origin", visible)
        self.assertIn("ownerNode.Scale = originalScale", visible)

    def test_spark_separate_impact_hold(self):
        seq = source("Nodes/Combat/NShinGetterShiningSparkSequence.cs")
        before = seq.split("public async Task PlayToImpact", 1)[1].split("public Task PlayImpact", 1)[0]
        self.assertNotIn("Stage(0.08f", before)
        self.assertIn("if (_closed || !OwnsSprite()) return;", before)
        self.assertIn("public Task PlayImpact() => Stage(0.08f", seq)
        self.assertIn("Task.WhenAll(impactVisual, initialHit)", source("Models/Cards/SGC_ShiningSpark.cs"))


if __name__ == "__main__":
    unittest.main()
