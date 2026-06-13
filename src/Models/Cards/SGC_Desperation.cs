using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 绝境 | 能力 | 稀有 | 1费 | 钢之魂流key
/// 降低生命至 1，战斗结束回复等量 HP，本回合可以免费打出精神指令卡，保留，消耗
/// 一号机：获 5 攻；二号机：获 2 能量、抽 3；三号机：获 1 缓冲、1 人工制品
/// </summary>
public sealed class SGC_Desperation : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_Desperation()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 降低生命至 1，战斗结束回复等量 HP
        // TODO: 本回合可以免费打出精神指令卡
        // TODO: 形态加成
    }

    protected override void OnUpgrade()
    {
        // 1→0 费
    }
}
