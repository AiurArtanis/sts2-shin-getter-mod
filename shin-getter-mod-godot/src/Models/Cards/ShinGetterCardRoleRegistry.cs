#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Cards;

internal enum ShinGetterCardRole
{
    ResearchEvolution,
    Evolution,
    SpiritDrive,
    GetterRay,
    Strategy,
    GetterThreeDefense,
    GetterTwoSpeed,
    GetterOneCharge,
}

internal static class ShinGetterCardRoleRegistry
{
    private static readonly IReadOnlyDictionary<ShinGetterCardRole, IReadOnlySet<Type>> Roles =
        new Dictionary<ShinGetterCardRole, IReadOnlySet<Type>>
        {
            [ShinGetterCardRole.ResearchEvolution] = TypesOf<
                SGC_SaotomeBlueprint,
                SGC_EvolutionResonance,
                SGC_EvolutionEngine,
                SGC_InfiniteEvolution,
                SGC_GetterWill,
                SGC_GetterRayOverflow>(),
            [ShinGetterCardRole.Evolution] = TypesOf<
                SGC_EvolutionResonance,
                SGC_EvolutionEngine,
                SGC_InfiniteEvolution,
                SGC_GetterWill,
                SGC_GetterRayOverflow>(),
            [ShinGetterCardRole.SpiritDrive] = TypesOf<
                SGC_Ki,
                SGC_Spirit,
                SGC_HotBlood,
                SGC_SuperKi,
                SGC_FightingSpirit>(),
            [ShinGetterCardRole.GetterRay] = TypesOf<
                SGC_GetterBeam,
                SGC_FinalGetterBeam,
                SGC_GetterRayOverflow,
                SGC_StonerSunshine>(),
            [ShinGetterCardRole.Strategy] = TypesOf<
                SGC_BackupPlan,
                SGC_BoldPlan,
                SGC_Insight,
                SGC_SeizeFuture,
                SGC_TacticalRetreat>(),
            [ShinGetterCardRole.GetterThreeDefense] = TypesOf<
                SGC_Indomitable,
                SGC_IronWall,
                SGC_Guts,
                SGC_Avalanche,
                SGC_HedgehogTactic,
                SGC_PoseidonThunder>(),
            [ShinGetterCardRole.GetterTwoSpeed] = TypesOf<
                SGC_Acceleration,
                SGC_ShedLoad,
                SGC_BackupPlan,
                SGC_TacticalRetreat,
                SGC_GetterClaw,
                SGC_LigerAssault,
                SGC_SpiralDrill,
                SGC_TornadoDrill>(),
            [ShinGetterCardRole.GetterOneCharge] = TypesOf<
                SGC_GetterRush,
                SGC_DiveStrike,
                SGC_GetterElbow,
                SGC_GetterFlash,
                SGC_ShiftStrike,
                SGC_ChangeAttack>(),
        };

    internal static bool Has(CardModel card, ShinGetterCardRole role) =>
        Roles.TryGetValue(role, out IReadOnlySet<Type>? types)
        && types.Contains(card.GetType());

    internal static bool Has(IEnumerable<CardModel> cards, ShinGetterCardRole role) =>
        cards.Any(card => Has(card, role));

    private static IReadOnlySet<Type> TypesOf<T1, T2, T3, T4>() =>
        new HashSet<Type> { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };

    private static IReadOnlySet<Type> TypesOf<T1, T2, T3, T4, T5>() =>
        new HashSet<Type> { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) };

    private static IReadOnlySet<Type> TypesOf<T1, T2, T3, T4, T5, T6>() =>
        new HashSet<Type> { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6) };

    private static IReadOnlySet<Type> TypesOf<T1, T2, T3, T4, T5, T6, T7, T8>() =>
        new HashSet<Type>
        {
            typeof(T1), typeof(T2), typeof(T3), typeof(T4),
            typeof(T5), typeof(T6), typeof(T7), typeof(T8),
        };
}
