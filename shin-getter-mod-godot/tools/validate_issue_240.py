"""Static gate for mouse-transparent text tips on combat-pile selections."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
path = root / "src/Patches/ShinGetterCardSelectionHoverPatch.cs"
assert path.exists(), "combat-pile selection hover fix is missing"
patch = path.read_text(encoding="utf-8")
for expected in (
    "nameof(NHoverTipSet.SetAlignmentForCardHolder)",
    "holder.CardModel is not ShinGetterCardBase",
    "node is NCombatPileCardSelectScreen",
    'GetNodeOrNull<Control>("textHoverTipContainer")',
    "control.MouseFilter = Control.MouseFilterEnum.Ignore;",
    "foreach (Node child in node.GetChildren())",
    "MakeMouseTransparent(child);",
):
    assert expected in patch, expected
assert "QueueFree" not in patch and "Hide()" not in patch, "keep tip contents visible"
print("issue#240 static regression: PASS")
