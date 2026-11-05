"""Explicit native GainsBlock metadata, without broad enchantment overrides."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
cards = root / "src/Models/Cards"
eligible = (
    "SGC_BackupPlan", "SGC_BlackArmor", "SGC_DarkCape", "SGC_Defend",
    "SGC_Guts", "SGC_HedgehogTactic", "SGC_Jammer", "SGC_ShedLoad",
    "SGC_Specialization", "SGC_TacticalRetreat",
    "SGC_PressureBreath", "SGC_SeizeFuture",
)
missing = []
for name in eligible:
    source = (cards / f"{name}.cs").read_text(encoding="utf-8")
    assert "CardType.Skill" in source and "CreatureCmd.GainBlock(" in source, name
    if "public override bool GainsBlock => true;" not in source:
        missing.append(name)
assert not missing, f"native Nimble rejects these direct block skills: {missing}"
base = (cards / "ShinGetterCardBase.cs").read_text(encoding="utf-8")
assert "override bool GainsBlock" not in base, "do not infer eligibility for the whole card pool"
for name in ("SGC_ChosenOne", "SGC_IronWall", "SGC_ChainReaction", "SGC_Strike"):
    assert "public override bool GainsBlock => true;" not in (cards / f"{name}.cs").read_text(encoding="utf-8"), name
assert not list((root / "src/Patches").glob("*Fresnel*")), "retain native Fresnel/Nimble eligibility and lifecycle"
print(f"issue#284 source gate PASS ({len(eligible)} explicit skills); native reward/save runtime NOT_RUN")
