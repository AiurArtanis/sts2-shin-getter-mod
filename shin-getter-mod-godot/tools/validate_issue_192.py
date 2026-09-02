#!/usr/bin/env python3
"""Static regression gate for issue#192 Good Citizen Card persistence."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RELIC_PATH = ROOT / "src/Models/Relics/SGR_GoodCitizenCard.cs"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    relic = RELIC_PATH.read_text(encoding="utf-8-sig")

    require(
        "[SavedProperty]\n    public List<int> FreePurchaseActIndices" not in relic,
        "List<int> is not a supported SavedProperty type",
    )
    require(
        "public List<int> FreePurchaseActIndices => _freePurchaseActIndices;" in relic,
        "runtime purchase history must remain mutable for existing consumers",
    )
    require(
        "[SavedProperty]\n    private int[] SavedFreePurchaseActIndices" in relic,
        "purchase history must persist through a supported int[] proxy",
    )
    require(
        "get => _freePurchaseActIndices.ToArray();" in relic
        and "_freePurchaseActIndices.Clear();" in relic
        and "_freePurchaseActIndices.AddRange(value);" in relic,
        "the saved array proxy must round-trip the runtime purchase history",
    )
    require(
        "_freePurchaseActIndices = new List<int>();" in relic,
        "clones must retain an independent runtime purchase-history list",
    )

    print("issue#192 static validation passed")


if __name__ == "__main__":
    main()
