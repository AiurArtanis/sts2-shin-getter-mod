"""Source gate for acquiring Good Citizen during a paid relic transaction."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
source = (root / "src/Models/Relics/SGR_GoodCitizenCard.cs").read_text(encoding="utf-8")
price = source.split("public override decimal ModifyMerchantPrice", 1)[1].split("public override Task AfterItemPurchased", 1)[0]
assert re.search(r"entry\s+is\s+MerchantRelicEntry\s*\{\s*Model:\s*SGR_GoodCitizenCard\s*\}", price), "newly obtained relic must not reprice its own paid transaction to zero"
assert price.index("Model: SGR_GoodCitizenCard") < price.index("return 0m;"), "self-purchase exclusion must precede the discount"
assert "FreePurchaseActIndices.Contains(Owner.RunState.CurrentActIndex)" in source
assert "goldSpent != 0" in source, "paid transactions must not consume free purchase history"
assert "[SavedProperty]\n    private int[] SavedFreePurchaseActIndices" in source
print("issue#282 source gate PASS; native purchase/SL runtime NOT_RUN")
