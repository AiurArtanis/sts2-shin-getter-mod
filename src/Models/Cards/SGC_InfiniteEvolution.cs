#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_InfiniteEvolution : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_InfiniteEvolution>(1m) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    private int _permanentStrengthGain;
    private int _permanentDexterityGain;
    private int _permanentMaxHpGain;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PermanentStrengthGain
    {
        get => _permanentStrengthGain;
        set
        {
            AssertMutable();
            _permanentStrengthGain = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PermanentDexterityGain
    {
        get => _permanentDexterityGain;
        set
        {
            AssertMutable();
            _permanentDexterityGain = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PermanentMaxHpGain
    {
        get => _permanentMaxHpGain;
        set
        {
            AssertMutable();
            _permanentMaxHpGain = value;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var title = new LocString("static_hover_tips", "SHIN_GETTER_INFINITE_EVOLUTION_TOTAL.title");
            var description = new LocString("static_hover_tips", "SHIN_GETTER_INFINITE_EVOLUTION_TOTAL.description");
            description.Add("Strength", PermanentStrengthGain);
            description.Add("Dexterity", PermanentDexterityGain);
            description.Add("MaxHp", PermanentMaxHpGain);

            return WithContextualHoverTips(new IHoverTip[]
            {
                new HoverTip(title, description),
            });
        }
    }

    public SGC_InfiniteEvolution()
        : base(3, CardType.Skill, CardRarity.Event, TargetType.Self, false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_InfiniteEvolution>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    public override async Task BeforeCombatStart()
    {
        if (Owner?.Creature == null || Owner.PlayerCombatState == null)
            return;

        var ctx = new ThrowingPlayerChoiceContext();
        if (PermanentStrengthGain > 0)
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, PermanentStrengthGain, Owner.Creature, this, silent: true);
        if (PermanentDexterityGain > 0)
            await PowerCmd.Apply<DexterityPower>(ctx, Owner.Creature, PermanentDexterityGain, Owner.Creature, this, silent: true);
    }

    public void RecordVictoryGain(SGP_InfiniteEvolution.VictoryGain gain)
    {
        switch (gain)
        {
            case SGP_InfiniteEvolution.VictoryGain.Strength:
                PermanentStrengthGain++;
                break;
            case SGP_InfiniteEvolution.VictoryGain.Dexterity:
                PermanentDexterityGain++;
                break;
            case SGP_InfiniteEvolution.VictoryGain.MaxHp:
                PermanentMaxHpGain++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(gain), gain, null);
        }
    }
}
