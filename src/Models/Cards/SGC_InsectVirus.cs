using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_InsectVirus : ShinGetterCardBase
{
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<CurseCardPool>();
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Eternal, CardKeyword.Unplayable };
    public override bool HasTurnEndInHandEffect => true;

    protected override IEnumerable<string> ExtraRunAssetPaths =>
        NNightmareHandsVfx.AssetPaths.Concat(NSmokyVignetteVfx.AssetPaths);

    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_InsectVirus()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None, false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await ShinGetterCombatVfx.PlayInsectVirusNightmare(Owner.Creature);
        await PowerCmd.Apply<SGP_Wane>(choiceContext, Owner.Creature, 4m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
    }
}
