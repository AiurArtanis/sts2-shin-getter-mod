#!/usr/bin/env python3
"""Static regression gate for GitHub issue#32."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CARDS = ROOT / "src" / "Models" / "Cards"
POWERS = ROOT / "src" / "Models" / "Powers"
COMBAT = ROOT / "src" / "Nodes" / "Combat"
RELICS = ROOT / "src" / "Models" / "Relics"
LOCALIZATION = ROOT / "ShinGetterMod" / "localization"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def assert_manual_attack_vigor_lifecycle() -> None:
    manual_attack_cards = set()
    for path in CARDS.glob("SGC_*.cs"):
        source = read(path)
        if "CardType.Attack" in source and "CreatureCmd.Damage(" in source:
            manual_attack_cards.add(path.name)

    assert manual_attack_cards == {
        "SGC_GetterMissile.cs",
        "SGC_PetalBreakthrough.cs",
        "SGC_SpiralDrill.cs",
    }

    card_base = read(CARDS / "ShinGetterCardBase.cs")
    assert "CaptureVigorForManualAttack" in card_base
    assert "ConsumeCapturedVigor" in card_base

    for name in manual_attack_cards:
        source = read(CARDS / name)
        assert "CaptureVigorForManualAttack" in source
        assert "ConsumeCapturedVigor" in source
        assert "ConsumeForCardDamage" in source

    getter_beam = read(CARDS / "SGC_GetterBeam.cs")
    assert "DamageCmd.Attack(DynamicVars.CalculatedDamage)" in getter_beam
    assert ".FromCard(this" in getter_beam


def assert_infinite_evolution_lifecycle_and_weights() -> None:
    card = read(CARDS / "SGC_InfiniteEvolution.cs")
    assert "DeckVersion == null" in card
    assert "EnsureSharedProgressInitialized" in card
    assert "IsPrimaryCombatCopy" in card
    assert "GetSharedProgress" in card

    furnace = read(RELICS / "SGR_GetterFurnace.cs")
    fragment = read(RELICS / "SGR_EmperorsFragment.cs")
    for relic in (furnace, fragment):
        assert "InfiniteEvolutionProgressInitialized" in relic
        assert "InfiniteEvolutionStrengthGain" in relic
        assert "InfiniteEvolutionDexterityGain" in relic
        assert "InfiniteEvolutionMaxHpGain" in relic
    assert "InfiniteEvolutionProgressInitialized = getterFurnace.InfiniteEvolutionProgressInitialized" in fragment

    power = read(POWERS / "SGP_InfiniteEvolution.cs")
    assert "NextInt(100)" in power
    assert "< 18" in power
    assert "< 88" in power
    assert "NextInt(3)" not in power


def assert_fighting_spirit_and_indomitable_contracts() -> None:
    fighting_spirit = read(POWERS / "SGP_FightingSpirit.cs")
    assert "target == Owner && dealer != null && props.IsPoweredAttack() && Amount > 0" in fighting_spirit
    assert "CurrentSide" not in fighting_spirit

    indomitable = read(CARDS / "SGC_Indomitable.cs")
    assert "new PowerVar<SGP_Indomitable>(1m)" in indomitable
    assert 'DynamicVars["SGP_Indomitable"].UpgradeValueBy(1m)' in indomitable

    for locale in ("eng", "jpn", "zhs"):
        cards_json = read(LOCALIZATION / locale / "cards.json")
        assert '"S_G_C_INDOMITABLE.description"' in cards_json
        assert "{SGP_Indomitable:diff()}" in cards_json


def assert_animation_fixes() -> None:
    state_machine = read(COMBAT / "NShinGetterSpriteAnimationStateMachine.cs")
    assert "ShinDragonBlockScale" not in state_machine
    assert "ApplyOneShotScale" not in state_machine
    assert "HasTemporaryScale" not in state_machine

    star_slash = read(CARDS / "SGC_StarSlash.cs")
    assert "PlayHeavyCleave" in star_slash
    assert "firstHalfDurationOverride: 0.5f" in star_slash
    assert '.WithHitFx("vfx/vfx_giant_horizontal_slash")' in star_slash
    phased_animation = read(COMBAT / "NShinGetterStaticVisuals.cs")
    assert "float? firstHalfDurationOverride = null" in phased_animation
    assert "firstHalfDurationOverride" in phased_animation


def assert_ordered_transform_cards() -> None:
    for name, target in (("SGC_IronWall.cs", "Getter3"), ("SGC_Enable.cs", "Getter1")):
        source = read(CARDS / name)
        assert "TransformTo(" not in source
        assert f"!HasForm(Owner, ShinGetterForm.{target})" in source
        assert "await Transform(choiceContext, Owner, this)" in source


def main() -> None:
    assert_manual_attack_vigor_lifecycle()
    assert_infinite_evolution_lifecycle_and_weights()
    assert_fighting_spirit_and_indomitable_contracts()
    assert_animation_fixes()
    assert_ordered_transform_cards()
    print("issue#32 static regression: PASS")


if __name__ == "__main__":
    main()
