using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.CardPools;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 真盖塔卡牌基类。提供形态检测、变形等公共方法。
/// </summary>
public abstract class ShinGetterCardBase : CardModel
{
    public override CardPoolModel Pool => ModelDb.CardPool<ShinGetterCardPool>();

    public virtual ShinGetterForm CardForm => ShinGetterForm.None;
    public virtual int SpiritRequirement => 0;

    protected ShinGetterCardBase(
        int canonicalEnergyCost,
        CardType type,
        CardRarity rarity,
        TargetType targetType,
        bool shouldShowInCardLibrary = true)
        : base(canonicalEnergyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;

        if (!ReferenceEquals(card, this) || SpiritRequirement <= 0 || originalCost <= 0m)
            return false;

        int ki = card.Owner.Creature.GetPower<SGP_Ki>()?.Amount ?? 0;
        if (ki < SpiritRequirement)
            return false;

        modifiedCost = 0m;
        return true;
    }

    // ──── 形态检测 ────

    /// <summary>
    /// 获取当前玩家拥有的形态列表。真化形态同时返回所有三个形态。
    /// </summary>
    protected static ShinGetterForm[] GetCurrentForms(Player player)
    {
        var forms = new List<ShinGetterForm>();

        if (player.Creature.GetPower<SGP_ShinForm>() != null)
        {
            // 真化形态视作三个形态
            return new[] { ShinGetterForm.Getter1, ShinGetterForm.Getter2, ShinGetterForm.Getter3 };
        }

        if (player.Creature.GetPower<SGP_ShinGetterOne>() != null)
            forms.Add(ShinGetterForm.Getter1);
        if (player.Creature.GetPower<SGP_ShinGetterTwo>() != null)
            forms.Add(ShinGetterForm.Getter2);
        if (player.Creature.GetPower<SGP_ShinGetterThree>() != null)
            forms.Add(ShinGetterForm.Getter3);

        return forms.ToArray();
    }

    /// <summary>
    /// 检查当前是否处于指定形态（包含真化形态）。
    /// </summary>
    protected static bool HasForm(Player player, ShinGetterForm form)
    {
        return GetCurrentForms(player).Contains(form);
    }

    // ──── 变形 ────

    /// <summary>
    /// 变形到下一个形态：1→2→3→1。
    /// 真化形态下不变，改为同时触发三个形态的变形效果。
    /// </summary>
    public static async Task Transform(PlayerChoiceContext choiceContext, Player player, CardModel cardSource)
    {
        var creature = player.Creature;

        // 真化形态：不切换，改为同时触发三个形态的变形效果
        if (creature.GetPower<SGP_ShinForm>() != null)
        {
            await TriggerShinFormTransform(choiceContext, creature, cardSource);
            return;
        }

        var currentForms = GetCurrentForms(player);
        ShinGetterForm current = ShinGetterForm.None;

        // 确定当前形态
        if (currentForms.Contains(ShinGetterForm.Getter1) && !currentForms.Contains(ShinGetterForm.Getter2))
            current = ShinGetterForm.Getter1;
        else if (currentForms.Contains(ShinGetterForm.Getter2) && !currentForms.Contains(ShinGetterForm.Getter3))
            current = ShinGetterForm.Getter2;
        else if (currentForms.Contains(ShinGetterForm.Getter3))
            current = ShinGetterForm.Getter3;

        // 移除所有旧形态
        await RemoveAllFormPowers(choiceContext, creature);

        // 确定下一个形态
        ShinGetterForm next = current switch
        {
            ShinGetterForm.Getter1 => ShinGetterForm.Getter2,
            ShinGetterForm.Getter2 => ShinGetterForm.Getter3,
            _ => ShinGetterForm.Getter1,
        };

        // 应用新形态
        await ApplyFormPower(choiceContext, creature, next, player, cardSource);

        // 通知天选之子：变形计数+1
        var chosenOne = creature.GetPower<SGP_ChosenOne>();
        if (chosenOne != null)
            await chosenOne.OnTransform(creature);
    }

    /// <summary>
    /// 真化形态下的变形：不切换形态，但触发三个形态的出场效果。
    /// </summary>
    private static async Task TriggerShinFormTransform(PlayerChoiceContext choiceContext, Creature creature, CardModel cardSource)
    {
        // 一号机效果：获得1活力
        await PowerCmd.Apply<VigorPower>(
            choiceContext, creature, 1m, creature, cardSource);

        // 二号机效果：+1能量（通过直接给予能量）
        if (creature.Player != null)
            await PlayerCmd.GainEnergy(1, creature.Player);

        // 三号机效果：格挡→覆甲（形态切换不影响属性，仅触发格挡转化）
        // 三号机的 -2力/-2敏 只在形态变更时触发，真化形态不变不触发

        // 通知天选之子：变形计数+1
        var chosenOne = creature.GetPower<SGP_ChosenOne>();
        if (chosenOne != null)
            await chosenOne.OnTransform(creature);
    }

    private static async Task RemoveAllFormPowers(PlayerChoiceContext choiceContext, Creature creature)
    {
        var one = creature.GetPower<SGP_ShinGetterOne>();
        var two = creature.GetPower<SGP_ShinGetterTwo>();
        var three = creature.GetPower<SGP_ShinGetterThree>();
        var shin = creature.GetPower<SGP_ShinForm>();

        if (one != null) await PowerCmd.Remove(one);
        if (two != null) await PowerCmd.Remove(two);
        if (three != null) await PowerCmd.Remove(three);
        if (shin != null) await PowerCmd.Remove(shin);
    }

    private static async Task ApplyFormPower(PlayerChoiceContext choiceContext, Creature creature, ShinGetterForm form, Player player, CardModel cardSource)
    {
        switch (form)
        {
            case ShinGetterForm.Getter1:
                await PowerCmd.Apply<SGP_ShinGetterOne>(choiceContext, creature, 1m, creature, cardSource);
                break;
            case ShinGetterForm.Getter2:
                await PowerCmd.Apply<SGP_ShinGetterTwo>(choiceContext, creature, 1m, creature, cardSource);
                break;
            case ShinGetterForm.Getter3:
                await PowerCmd.Apply<SGP_ShinGetterThree>(choiceContext, creature, 1m, creature, cardSource);
                break;
        }
    }

    // ──── 状态层数查询 ────

    protected int GetPowerAmount<T>(Player player) where T : PowerModel
    {
        return player.Creature.GetPower<T>()?.Amount ?? 0;
    }
}

/// <summary>
/// 盖塔三种形态枚举。
/// </summary>
public enum ShinGetterForm
{
    None = 0,
    Getter1 = 1,
    Getter2 = 2,
    Getter3 = 3,
}
