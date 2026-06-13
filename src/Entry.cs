using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod;

[ModInitializer("Init")]
public static class Entry
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // Register all Harmony patches (CharacterListPatch)
        Harmony harmony = new Harmony("Artanis.ShinGetterMod");
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
            {
                new PatchClassProcessor(harmony, type).Patch();
            }
        }

        // Register 68+3 cards to the ColorlessCardPool
        AddCardsToPool();

        Log.Info("ShinGetterMod - loading success! (68+3 cards)");
    }

    private static void AddCardsToPool()
    {
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Strike>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Defend>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterBeam>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterLaunch>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_DiveStrike>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_HurricaneStrike>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterChop>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterElbow>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_ChangeStrike>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_FocusFire>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterTomahawk>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterDrill>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterRush>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Meltdown>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Vigor>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Indomitable>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_TacticalRetreat>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_BlackArmor>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_ShedLoad>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_HedgehogTactic>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_ChangeAttack>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Annihilation>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_HotBlood>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_TomahawkFury>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterRayOverflow>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_TornadoDrill>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_SpiralDrill>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_ExpansionStrike>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterMissile>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_EvolutionResonance>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_SeizeFuture>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Spirit>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_PartsSwap>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_TripleUnity>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_DarkCape>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_BackupPlan>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Grapple>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Jammer>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Overload>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Acceleration>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Insight>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_ChosenOne>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_SaotomeBlueprint>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_WarriorMedal>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_FightingSpirit>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_ChainReaction>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_StarSlash>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_FinalGetterBeam>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_FlashBurst>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_LigerAssault>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Avalanche>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_PoseidonThunder>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_SteelSpirit>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterWill>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Specialization>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_IronWall>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Guts>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterNova>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_BoldPlan>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_GetterRayBurst>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_AwakenedSoul>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_SuperKi>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_EvolutionEngine>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Enable>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_AntiEvolution>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_Desperation>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_StonerShine>();
        ModHelper.AddModelToPool<ColorlessCardPool, SGC_ShinForm>();
    }
}
