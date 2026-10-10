"""Static persistence and localization gate for one free purchase per act map."""
from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
relic = (root / "src/Models/Relics/SGR_GoodCitizenCard.cs").read_text(encoding="utf-8")
assert "FreePurchaseActIndices.Contains(Owner.RunState.CurrentActIndex)" in relic, "eligibility must use saved map history"
assert "LastFreeFloor == Owner.RunState.TotalFloor" not in relic, "a new floor must not restore the free purchase"
assert "[SavedProperty]\n    private int[] SavedFreePurchaseActIndices" in relic
assert "_freePurchaseActIndices.AddRange(value);" in relic, "existing saves must retain map history"
assert "Status = IsUsedThisAct ? RelicStatus.Disabled : RelicStatus.Normal;" in relic
assert "IsUsedThisAct || goldSpent != 0" in relic, "paid purchases must not add free-purchase history"
assert "FreePurchaseActIndices.Add(Owner.RunState.CurrentActIndex);" in relic
descriptions = {
    "zhs": "每张地图限一次",
    "eng": "Once per act map",
    "jpn": "各マップ1回まで",
}
for lang, expected in descriptions.items():
    data = json.loads((root / f"ShinGetterMod/localization/{lang}/relics.json").read_text(encoding="utf-8"))
    assert expected in data["S_G_R_GOOD_CITIZEN_CARD.description"], lang
print("issue#242 static regression: PASS")
