using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 绝境 | 能力 | 稀有 | 1费 | 钢之魂流key
/// 获得 3 进化和 3 气力。降低生命至 1，战斗结束回复等量 HP
/// 一号机：获 5 攻；二号机：获 2 能量、抽 3；三号机：获 1 缓冲、1 人工制品
/// </summary>
public sealed class SGC_Desperation : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_Desperation>(),
        HoverTipFactory.FromPower<SGP_Ki>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<BufferPower>(),
        HoverTipFactory.FromPower<ArtifactPower>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Evolution>(3m),
        new PowerVar<SGP_Ki>(3m),
        new PowerVar<StrengthPower>(5m),
        new EnergyVar(2),
        new CardsVar(3),
        new PowerVar<BufferPower>(1m),
        new PowerVar<ArtifactPower>(1m),
    };

    public SGC_Desperation()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_Evolution>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SGP_Evolution"].BaseValue,
            Owner.Creature,
            this);

        await PowerCmd.Apply<SGP_Ki>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SGP_Ki"].BaseValue,
            Owner.Creature,
            this);

        int hpLost = Math.Max(Owner.Creature.CurrentHp - 1, 0);
        if (hpLost > 0)
        {
            using (ShinGetterVoiceService.SuppressLowHpThresholdVoices(Owner))
                await CreatureCmd.SetCurrentHp(Owner.Creature, 1m);
            await PowerCmd.Apply<SGP_Desperation>(
                choiceContext, Owner.Creature, hpLost, Owner.Creature, this);
        }

        if (HasForm(Owner, ShinGetterForm.Getter1))
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
        if (HasForm(Owner, ShinGetterForm.Getter2))
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
        if (HasForm(Owner, ShinGetterForm.Getter3))
        {
            await PowerCmd.Apply<BufferPower>(choiceContext, Owner.Creature, DynamicVars["BufferPower"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner.Creature, DynamicVars["ArtifactPower"].BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
