"""Audited b0f76260 registry plus issue#238 additions; never accept a count alone."""
import re

BASELINE_CARDS = frozenset("""
SGC_Strike SGC_Defend SGC_GetterBeam SGC_GetterLaunch SGC_DiveStrike
SGC_HurricaneStrike SGC_GetterChop SGC_GetterElbow SGC_ShiftStrike SGC_FocusFire
SGC_GetterTomahawk SGC_GetterClaw SGC_GetterRush SGC_Meltdown SGC_Ki
SGC_Indomitable SGC_TacticalRetreat SGC_BlackArmor SGC_ShedLoad SGC_HedgehogTactic
SGC_ChangeAttack SGC_Annihilation SGC_HotBlood SGC_TomahawkFury SGC_GetterFlash
SGC_TornadoDrill SGC_SpiralDrill SGC_ExpansionStrike SGC_GetterMissile SGC_EvolutionResonance
SGC_SeizeFuture SGC_Spirit SGC_PartsSwap SGC_TripleUnity SGC_DarkCape
SGC_BackupPlan SGC_Grapple SGC_Jammer SGC_Overload SGC_Acceleration
SGC_Insight SGC_ChosenOne SGC_SaotomeBlueprint SGC_WarriorMedal SGC_FightingSpirit
SGC_ChainReaction SGC_StarSlash SGC_FinalGetterBeam SGC_ShiningSpark SGC_LigerAssault
SGC_Avalanche SGC_PoseidonThunder SGC_SteelSpirit SGC_GetterWill SGC_Specialization
SGC_IronWall SGC_Guts SGC_GetterNova SGC_BoldPlan SGC_GetterRayOverflow
SGC_AwakenedSoul SGC_SuperKi SGC_EvolutionEngine SGC_Enable SGC_AntiEvolution
SGC_Desperation SGC_StonerSunshine SGC_ShinForm SGC_SaintDragonRoar SGC_Radiated
SGC_InsectVirus SGC_InfiniteEvolution SGC_PetalBreakthrough SGC_RescheduleTicket
SGC_PressureBreath SGC_WispCoordinate SGC_GetterLanding
""".split())
ISSUE238_CARDS = frozenset({"SGC_TabletOfTruth", "SGC_TripleWhirlwind", "SGC_DrillMissile",
                           "SGC_BrokenDrill", "SGC_Annotations"})


def validate_registered_cards(pool):
    entries = re.findall(r"ModelDb\.Card<([^>]+)>\(\)", pool)
    assert len(BASELINE_CARDS) == 77 and len(ISSUE238_CARDS) == 5
    assert len(entries) == len(set(entries)) == 82, "duplicate/missing card registration"
    assert set(entries) == BASELINE_CARDS | ISSUE238_CARDS, "baseline 77 card IDs must remain"
    for model in ISSUE238_CARDS:
        assert f"not {model}" in pool, f"event/status card entered normal rewards: {model}"
    return entries
