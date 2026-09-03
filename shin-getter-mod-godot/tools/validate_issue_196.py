from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BASE = (ROOT / "src/Models/Cards/ShinGetterCardBase.cs").read_text(encoding="utf-8")
CARD = (ROOT / "src/Models/Cards/SGC_HedgehogTactic.cs").read_text(encoding="utf-8")

registration = '["SGC_HedgehogTactic"] = new[] { "格挡", "活力", "三号机" }'
if registration not in BASE:
    raise AssertionError("issue#196: Hedgehog Tactic must register the Getter 3 glow term.")
if "GetGlowFormsForCard().Any(form => HasForm(Owner, form))" not in BASE:
    raise AssertionError("issue#196: form glow must use the same HasForm predicate as card effects.")
if "return new[] { ShinGetterForm.Getter1, ShinGetterForm.Getter2, ShinGetterForm.Getter3 };" not in BASE:
    raise AssertionError("issue#196: Shin Getter Dragon must continue to count as every atomic form.")
if "HasForm(Owner, ShinGetterForm.Getter3)" not in CARD:
    raise AssertionError("issue#196: Hedgehog Tactic effect predicate changed unexpectedly.")

print("issue#196 static regression: PASS")
