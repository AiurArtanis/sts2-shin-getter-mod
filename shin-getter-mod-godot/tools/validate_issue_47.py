#!/usr/bin/env python3
"""Static regression gate for issue#47 / B1.1.1."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8-sig")


def require(relative_path: str, *fragments: str) -> None:
    text = read(relative_path)
    for fragment in fragments:
        if fragment not in text:
            raise AssertionError(f"{relative_path}: missing {fragment!r}")


def reject(relative_path: str, *fragments: str) -> None:
    text = read(relative_path)
    for fragment in fragments:
        if fragment in text:
            raise AssertionError(f"{relative_path}: forbidden {fragment!r}")


def validate_code() -> None:
    require(
        "src/Models/Cards/ShinGetterCardBase.cs",
        "protected virtual float ActionAnimationSpeedScale => 1f;",
        "QueueNextActionSpeed(creature, ActionAnimationSpeedScale)",
    )
    require(
        "src/Models/Cards/SGC_ShinForm.cs",
        "protected override float ActionAnimationSpeedScale => 0.5f;",
    )
    require("src/Models/Cards/SGC_ChosenOne.cs", ": base(2, CardType.Power")
    require(
        "src/Models/Cards/SGC_BoldPlan.cs",
        "IEnumerable<CardModel> drawnCards = await CardPileCmd.Draw",
        "CardCmd.ApplyKeyword(drawnCard, CardKeyword.Retain)",
        "HoverTipFactory.FromKeyword(CardKeyword.Retain)",
        ".Concat(HoverTipFactory.FromEnchantment<SGE_Adaptation>())",
    )
    require(
        "src/Models/Cards/SGC_GetterWill.cs",
        "using MegaCrit.Sts2.Core.Models;",
        "new CardsVar(1)",
        "DynamicVars.Cards.IntValue",
        "await CardPileCmd.Add(selected, PileType.Hand)",
        "DynamicVars.Cards.UpgradeValueBy(1m)",
    )
    reject("src/Models/Cards/SGC_GetterWill.cs", "RemoveKeyword(CardKeyword.Exhaust)")
    require(
        "src/Models/Cards/SGC_EvolutionEngine.cs",
        "PowerCmd.Apply<SGP_EvolutionEngine>",
        "PowerCmd.Apply<SGP_Evolution>",
        'DynamicVars["SGP_Evolution"].UpgradeValueBy(1m)',
    )
    reject(
        "src/Models/Cards/SGC_EvolutionEngine.cs",
        "EvolutionEngineEnergy",
        'DynamicVars["SGP_EvolutionEngine"].UpgradeValueBy',
    )
    require(
        "src/Models/Powers/SGP_EvolutionEngine.cs",
        "PowerStackType.Single",
        "AfterPowerAmountChanged",
        "power is not SGP_Evolution",
        "amount == 0m",
        "ShinGetterCardBase.Transform(choiceContext, player, cardSource)",
    )
    reject(
        "src/Models/Powers/SGP_EvolutionEngine.cs",
        "pendingEnergyGain",
        "GainEnergy",
        "AfterPlayerTurnStartEarly",
    )
    reject("src/Models/Powers/SGP_Evolution.cs", "MarkPendingEnergyGain")


def validate_localization() -> None:
    required = {
        "zhs": ("保留", "{Cards:diff()}张能力牌", "层数变更", "变形"),
        "eng": ("Retain", "{Cards:diff()} Power card", "Evolution", "Transform"),
        "jpn": ("保留", "{Cards:diff()}枚", "進化", "変形"),
    }
    for language, fragments in required.items():
        cards = json.loads(read(f"ShinGetterMod/localization/{language}/cards.json"))
        powers = json.loads(read(f"ShinGetterMod/localization/{language}/powers.json"))
        combined = "\n".join(
            (
                cards["S_G_C_BOLD_PLAN.description"],
                cards["S_G_C_GETTER_WILL.description"],
                cards["S_G_C_GETTER_WILL.selectionScreenPrompt"],
                cards["S_G_C_EVOLUTION_ENGINE.description"],
                powers["S_G_P_EVOLUTION_ENGINE.description"],
            )
        )
        for fragment in fragments:
            if fragment not in combined:
                raise AssertionError(f"{language}: missing {fragment!r}")
        if "EvolutionEngineEnergy" in combined or "next turn" in combined.lower():
            raise AssertionError(f"{language}: stale Evolution Engine energy text")


def main() -> None:
    validate_code()
    validate_localization()
    print("issue#47 static validation passed")


if __name__ == "__main__":
    main()
