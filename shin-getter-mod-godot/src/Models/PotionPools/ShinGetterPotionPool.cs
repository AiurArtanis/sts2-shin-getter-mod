using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Unlocks;
using ShinGetterMod.Models.Potions;

namespace ShinGetterMod.Models.PotionPools;

public sealed class ShinGetterPotionPool : PotionPoolModel
{
    public override string EnergyColorName => "shin_getter";
    public override Color LabOutlineColor => StsColors.aqua;

    private static IEnumerable<PotionModel> CustomPotions
    {
        get
        {
            yield return ModelDb.Potion<SGR_TransformPotion>();
            yield return ModelDb.Potion<SGR_KusuhaJuice>();
            yield return ModelDb.Potion<SGR_GetterColdBrew>();
        }
    }

    protected override IEnumerable<PotionModel> GenerateAllPotions()
    {
        return WeightedCustomPotions(weight: 2)
            .Concat(EventPotions);
    }

    public override IEnumerable<PotionModel> GetUnlockedPotions(UnlockState unlockState)
    {
        return WeightedCustomPotions(weight: 2);
    }

    private static IEnumerable<PotionModel> WeightedCustomPotions(int weight)
    {
        foreach (PotionModel potion in CustomPotions)
        {
            for (int i = 0; i < weight; i++)
                yield return potion;
        }
    }

    private static IEnumerable<PotionModel> EventPotions
    {
        get
        {
            yield return ModelDb.Potion<SGR_LuminescentPulse>();
            yield return ModelDb.Potion<SGR_PhaseCoolant>();
            yield return ModelDb.Potion<SGR_AdaptiveInk>();
        }
    }
}
