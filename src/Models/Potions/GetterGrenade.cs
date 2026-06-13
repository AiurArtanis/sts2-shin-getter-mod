#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Potions;

/// <summary>
/// 盖塔手雷 — 对所有敌人造成 20 点伤害。
/// </summary>
public sealed class GetterGrenade : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AllEnemies;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(20m, ValueProp.Unpowered)
    };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        await CreatureCmd.Damage(
            choiceContext,
            combatState.HittableEnemies,
            DynamicVars.Damage.BaseValue,
            DynamicVars.Damage.Props,
            Owner.Creature,
            null);
    }
}
