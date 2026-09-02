#!/usr/bin/env python3
"""Static regression gate for issue#191 multiplayer power synchronization."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8-sig")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    patch = read("src/Patches/VigorPowerSetAmountPatch.cs")
    chain = read("src/Models/Powers/SGP_ChainReaction.cs")

    require("async void" not in patch,
            "Vigor SetAmount patch must not start unawaitable combat mutations")
    require("PowerCmd.Apply" not in patch and "ThrowingPlayerChoiceContext" not in patch,
            "Vigor SetAmount patch must only perform synchronous tracking cleanup")
    require("ResetAttackTracking(__instance);" in patch,
            "Vigor attack tracking cleanup must remain in the low-level SetAmount patch")

    require("public override async Task AfterPowerAmountChanged(" in chain,
            "Chain Reaction must use the awaited power-change hook")
    for fragment in (
        "power is not VigorPower",
        "power.Owner != Owner",
        "amount >= 0m",
        "Owner.IsDead",
        "await PowerCmd.Apply<RegenPower>(choiceContext, Owner, gain, Owner, null);",
        "await PowerCmd.Apply<PlatingPower>(choiceContext, Owner, gain, Owner, null);",
    ):
        require(fragment in chain, f"Chain Reaction is missing {fragment!r}")
    require("new ThrowingPlayerChoiceContext" not in chain,
            "Chain Reaction must preserve the caller's synchronized choice context")

    print("issue#191 static validation passed")


if __name__ == "__main__":
    main()
