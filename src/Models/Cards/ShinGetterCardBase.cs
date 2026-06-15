#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
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
    private static readonly IReadOnlyDictionary<string, Func<CardModel, IHoverTip>> TermTips =
        new Dictionary<string, Func<CardModel, IHoverTip>>
        {
            ["格挡"] = _ => HoverTipFactory.Static(StaticHoverTip.Block),
            ["能量"] = card => HoverTipFactory.ForEnergy(card),
            ["活力"] = _ => HoverTipFactory.FromPower<VigorPower>(),
            ["再生"] = _ => HoverTipFactory.FromPower<RegenPower>(),
            ["覆甲"] = _ => HoverTipFactory.FromPower<PlatingPower>(),
            ["易伤"] = _ => HoverTipFactory.FromPower<VulnerablePower>(),
            ["虚弱"] = _ => HoverTipFactory.FromPower<WeakPower>(),
            ["脆弱"] = _ => HoverTipFactory.FromPower<FrailPower>(),
            ["敏捷"] = _ => HoverTipFactory.FromPower<DexterityPower>(),
            ["力量"] = _ => HoverTipFactory.FromPower<StrengthPower>(),
            ["荆棘"] = _ => HoverTipFactory.FromPower<ThornsPower>(),
            ["缓冲"] = _ => HoverTipFactory.FromPower<BufferPower>(),
            ["人工制品"] = _ => HoverTipFactory.FromPower<ArtifactPower>(),
            ["气力"] = _ => HoverTipFactory.FromPower<SGP_Ki>(),
            ["腾空"] = _ => HoverTipFactory.FromPower<SGP_Airborne>(),
            ["进化"] = _ => HoverTipFactory.FromPower<SGP_Evolution>(),
            ["辐射"] = _ => HoverTipFactory.FromPower<SGP_Radiation>(),
            ["衰退"] = _ => HoverTipFactory.FromPower<SGP_Wane>(),
            ["封印"] = _ => HoverTipFactory.FromPower<SGP_Seal>(),
            ["分身"] = _ => HoverTipFactory.FromPower<SGP_Shade>(),
            ["一号机"] = _ => HoverTipFactory.FromPower<SGP_ShinGetterOne>(),
            ["二号机"] = _ => HoverTipFactory.FromPower<SGP_ShinGetterTwo>(),
            ["三号机"] = _ => HoverTipFactory.FromPower<SGP_ShinGetterThree>(),
            ["真化形态"] = _ => HoverTipFactory.FromPower<SGP_ShinForm>(),
            ["气势"] = _ => HoverTipFactory.FromCard<SGC_Ki>(),
            ["放射能"] = _ => HoverTipFactory.FromCard<SGC_Radiated>(),
            ["变形"] = _ => CustomTip("SHIN_GETTER_TRANSFORM"),
            ["精神指令卡"] = _ => CustomTip("SHIN_GETTER_SPIRIT_COMMAND"),
        };

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

    protected override IEnumerable<IHoverTip> ExtraHoverTips => ContextualHoverTips;

    protected IEnumerable<IHoverTip> ContextualHoverTips
    {
        get
        {
            string description = Description.GetRawText();
            return TermTips
                .Where(pair => description.Contains(pair.Key, StringComparison.Ordinal))
                .Select(pair => pair.Value(this));
        }
    }

    protected IEnumerable<IHoverTip> WithContextualHoverTips(IEnumerable<IHoverTip> tips) =>
        IHoverTip.RemoveDupes(tips.Concat(ContextualHoverTips));

    private static IHoverTip CustomTip(string key) => new HoverTip(
        new LocString("static_hover_tips", key + ".title"),
        new LocString("static_hover_tips", key + ".description"));

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

    public static bool IsInForm(Player player, ShinGetterForm form) => HasForm(player, form);

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

        PowerModel? currentPower = creature.GetPower<SGP_ShinGetterOne>()
            ?? (PowerModel?)creature.GetPower<SGP_ShinGetterTwo>()
            ?? creature.GetPower<SGP_ShinGetterThree>();
        ShinGetterForm current = currentPower switch
        {
            SGP_ShinGetterOne => ShinGetterForm.Getter1,
            SGP_ShinGetterTwo => ShinGetterForm.Getter2,
            SGP_ShinGetterThree => ShinGetterForm.Getter3,
            _ => ShinGetterForm.None,
        };

        if (currentPower != null)
            await PowerCmd.Remove(currentPower);

        // 确定下一个形态
        ShinGetterForm next = current switch
        {
            ShinGetterForm.Getter1 => ShinGetterForm.Getter2,
            ShinGetterForm.Getter2 => ShinGetterForm.Getter3,
            _ => ShinGetterForm.Getter1,
        };

        // The form power remains the first visible state throughout normal play.
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
        // 真化形态只触发一次“发生了变形”的事件，不切换或重复施加形态入场效果。
        var chosenOne = creature.GetPower<SGP_ChosenOne>();
        if (chosenOne != null)
            await chosenOne.OnTransform(creature);
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
