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
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Models.Relics;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Patches;

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
            ["精神"] = _ => CustomTip("SHIN_GETTER_SPIRIT_COMMAND"),
            ["精神指令卡"] = _ => CustomTip("SHIN_GETTER_SPIRIT_COMMAND"),
            ["专属形态卡"] = _ => CustomTip("SHIN_GETTER_FORM_CARD"),
            ["真盖塔龙"] = _ => HoverTipFactory.FromPower<SGP_ShinForm>(),
        };

    private static readonly IReadOnlyDictionary<string, string[]> CardDescriptionTerms =
        new Dictionary<string, string[]>
        {
            ["SGC_Acceleration"] = new[] { "精神" },
            ["SGC_Annihilation"] = new[] { "放射能", "衰退" },
            ["SGC_AntiEvolution"] = new[] { "封印" },
            ["SGC_Avalanche"] = new[] { "格挡", "覆甲", "三号机" },
            ["SGC_AwakenedSoul"] = new[] { "精神" },
            ["SGC_BackupPlan"] = new[] { "二号机", "能量" },
            ["SGC_BlackArmor"] = new[] { "格挡", "一号机", "易伤", "虚弱", "脆弱" },
            ["SGC_BoldPlan"] = new[] { "辐射", "气力", "能量" },
            ["SGC_ChainReaction"] = new[] { "活力", "再生", "覆甲" },
            ["SGC_ChangeAttack"] = new[] { "变形" },
            ["SGC_ShiftStrike"] = new[] { "变形", "活力", "再生", "覆甲" },
            ["SGC_ChosenOne"] = new[] { "变形", "气力" },
            ["SGC_DarkCape"] = new[] { "格挡", "一号机", "腾空" },
            ["SGC_Defend"] = new[] { "格挡" },
            ["SGC_Desperation"] = new[] { "精神指令卡", "力量", "缓冲", "人工制品" },
            ["SGC_DiveStrike"] = new[] { "腾空", "一号机" },
            ["SGC_Enable"] = new[] { "精神" },
            ["SGC_EvolutionEngine"] = new[] { "进化", "能量" },
            ["SGC_EvolutionResonance"] = new[] { "进化" },
            ["SGC_ExpansionStrike"] = new[] { "三号机", "覆甲" },
            ["SGC_FightingSpirit"] = new[] { "精神" },
            ["SGC_FinalGetterBeam"] = new[] { "力量" },
            ["SGC_ShiningSpark"] = new[] { "易伤", "脆弱", "气力" },
            ["SGC_GetterBeam"] = new[] { "衰退", "一号机", "活力" },
            ["SGC_GetterChop"] = new[] { "格挡" },
            ["SGC_GetterClaw"] = new[] { "二号机" },
            ["SGC_GetterLaunch"] = new[] { "气力", "变形" },
            ["SGC_GetterMissile"] = new[] { "三号机", "格挡" },
            ["SGC_GetterNova"] = new[] { "活力", "辐射" },
            ["SGC_GetterFlash"] = new[] { "活力", "腾空", "一号机" },
            ["SGC_GetterRush"] = new[] { "易伤", "覆甲", "三号机" },
            ["SGC_GetterTomahawk"] = new[] { "一号机", "活力" },
            ["SGC_GetterWill"] = new[] { "一号机" },
            ["SGC_Grapple"] = new[] { "虚弱", "三号机", "力量" },
            ["SGC_Guts"] = new[] { "精神", "格挡" },
            ["SGC_HedgehogTactic"] = new[] { "格挡", "活力" },
            ["SGC_HotBlood"] = new[] { "精神" },
            ["SGC_HurricaneStrike"] = new[] { "二号机", "敏捷" },
            ["SGC_Indomitable"] = new[] { "易伤" },
            ["SGC_InfiniteEvolution"] = new[] { "力量", "敏捷" },
            ["SGC_InsectVirus"] = new[] { "衰退" },
            ["SGC_Insight"] = new[] { "精神", "敏捷", "力量", "荆棘" },
            ["SGC_IronWall"] = new[] { "精神", "三号机", "覆甲" },
            ["SGC_Jammer"] = new[] { "分身", "变形", "二号机" },
            ["SGC_Ki"] = new[] { "气力", "活力" },
            ["SGC_LigerAssault"] = new[] { "二号机", "分身" },
            ["SGC_Meltdown"] = new[] { "放射能" },
            ["SGC_Overload"] = new[] { "能量" },
            ["SGC_PoseidonThunder"] = new[] { "易伤", "虚弱", "脆弱" },
            ["SGC_Radiated"] = new[] { "进化", "辐射" },
            ["SGC_SaotomeBlueprint"] = new[] { "进化" },
            ["SGC_SeizeFuture"] = new[] { "格挡" },
            ["SGC_ShedLoad"] = new[] { "气力", "敏捷", "再生", "二号机" },
            ["SGC_ShinForm"] = new[] { "真盖塔龙" },
            ["SGC_Specialization"] = new[] { "专属形态卡", "二号机" },
            ["SGC_SpiralDrill"] = new[] { "二号机", "格挡" },
            ["SGC_Spirit"] = new[] { "精神", "气势" },
            ["SGC_StarSlash"] = new[] { "一号机", "活力" },
            ["SGC_SteelSpirit"] = new[] { "精神指令卡" },
            ["SGC_StonerSunshine"] = new[] { "衰退", "活力" },
            ["SGC_SuperKi"] = new[] { "活力", "气势" },
            ["SGC_TacticalRetreat"] = new[] { "格挡", "变形" },
            ["SGC_TomahawkFury"] = new[] { "活力", "一号机" },
            ["SGC_TornadoDrill"] = new[] { "二号机", "格挡" },
            ["SGC_TripleUnity"] = new[] { "变形" },
            ["SGC_WarriorMedal"] = new[] { "气力", "再生", "覆甲" },
        };

    private static readonly IReadOnlyDictionary<string, ShinGetterForm> FormGlowTerms =
        new Dictionary<string, ShinGetterForm>
        {
            ["一号机"] = ShinGetterForm.Getter1,
            ["二号机"] = ShinGetterForm.Getter2,
            ["三号机"] = ShinGetterForm.Getter3,
        };

    private static readonly IReadOnlySet<string> DashAnimationCards =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SGC_ChangeAttack",
            "SGC_DiveStrike",
            "SGC_GetterRush",
            "SGC_LigerAssault",
            "SGC_ShiftStrike",
        };

    private static readonly IReadOnlySet<string> CastAttackAnimationCards =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SGC_FinalGetterBeam",
            "SGC_GetterBeam",
            "SGC_GetterFlash",
            "SGC_HolyDragonRoar",
            "SGC_PoseidonThunder",
            "SGC_ShiningSpark",
            "SGC_StonerSunshine",
        };

    private static readonly IReadOnlySet<string> BlockAnimationCards =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SGC_BlackArmor",
            "SGC_DarkCape",
            "SGC_Defend",
            "SGC_Guts",
            "SGC_HedgehogTactic",
            "SGC_IronWall",
            "SGC_SeizeFuture",
            "SGC_TacticalRetreat",
        };

    public override CardPoolModel Pool => ModelDb.CardPool<ShinGetterCardPool>();

    public virtual ShinGetterForm CardForm => ShinGetterForm.None;
    public virtual int SpiritRequirement => 0;
    public virtual int UpgradePreviewSpiritRequirement => SpiritRequirement;

    protected override bool ShouldGlowGoldInternal => IsFormGlowActive();

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
            CardDescriptionTerms.TryGetValue(GetType().Name, out string[]? registeredTerms);
            return TermTips.Keys
                .Where(term => description.Contains(term, StringComparison.Ordinal))
                .Concat(registeredTerms ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .Where(TermTips.ContainsKey)
                .Select(term => TermTips[term](this));
        }
    }

    protected IEnumerable<IHoverTip> WithContextualHoverTips(IEnumerable<IHoverTip> tips) =>
        IHoverTip.RemoveDupes(tips.Concat(ContextualHoverTips));

    public override Task OnEnqueuePlayVfx(Creature? target)
    {
        if (Owner?.Creature is not { } creature)
            return Task.CompletedTask;

        string? animationTrigger = GetActionAnimationTrigger();
        if (animationTrigger != null)
            NShinGetterStaticVisuals.TryPlayCreatureActionAnimation(creature, animationTrigger);

        if (this is SGC_ShinForm)
            NShinGetterStaticVisuals.PlayShinFormTransformVfx(creature);

        return Task.CompletedTask;
    }

    private string? GetActionAnimationTrigger()
    {
        string cardTypeName = GetType().Name;
        if (BlockAnimationCards.Contains(cardTypeName))
            return "Block";

        if (Type == CardType.Attack)
        {
            if (DashAnimationCards.Contains(GetType().Name))
                return "Dash";

            if (CastAttackAnimationCards.Contains(GetType().Name))
                return "Cast";

            return "Attack";
        }

        if (Type is CardType.Skill or CardType.Power)
            return "Cast";

        return null;
    }

    private static IHoverTip CustomTip(string key) => new HoverTip(
        new LocString("static_hover_tips", key + ".title"),
        new LocString("static_hover_tips", key + ".description"));

    private bool IsFormGlowActive()
    {
        if (Pile?.Type != PileType.Hand || CombatState == null)
            return false;

        return GetGlowFormsForCard().Any(form => HasForm(Owner, form));
    }

    private IEnumerable<ShinGetterForm> GetGlowFormsForCard()
    {
        if (CardDescriptionTerms.TryGetValue(GetType().Name, out string[]? registeredTerms))
        {
            foreach (ShinGetterForm form in GetGlowFormsFromTerms(registeredTerms))
                yield return form;
        }

        string description = Description.GetRawText();
        foreach (KeyValuePair<string, ShinGetterForm> formTerm in FormGlowTerms)
        {
            if (description.Contains(formTerm.Key, StringComparison.Ordinal))
                yield return formTerm.Value;
        }
    }

    private static IEnumerable<ShinGetterForm> GetGlowFormsFromTerms(IEnumerable<string> terms)
    {
        foreach (string term in terms)
        {
            if (FormGlowTerms.TryGetValue(term, out ShinGetterForm form))
                yield return form;
        }
    }

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;

        if (!ReferenceEquals(card, this) || SpiritRequirement <= 0 || originalCost <= 0m)
            return false;

        if (card.Owner.Creature.GetPower<SGP_KusuhaJuice>() != null)
        {
            modifiedCost = 0m;
            return true;
        }

        int ki = card.Owner.Creature.GetPower<SGP_Ki>()?.Amount ?? 0;
        if (ki < SpiritRequirement)
            return false;

        modifiedCost = Math.Max(0m, originalCost - 1m);
        return true;
    }

    // ──── 形态检测 ────

    /// <summary>
    /// 获取当前玩家拥有的形态列表。真化形态同时返回所有三个形态。
    /// </summary>
    protected static ShinGetterForm[] GetCurrentForms(Player player)
    {
        if (!IsShinGetterPlayer(player))
            return Array.Empty<ShinGetterForm>();

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
    public static async Task Transform(PlayerChoiceContext choiceContext, Player player, CardModel? cardSource)
    {
        if (!IsShinGetterPlayer(player))
        {
            return;
        }

        var creature = player.Creature;

        if (creature.GetPower<SGP_Seal>() is { } seal)
        {
            seal.FlashBlockedTransform();
            return;
        }

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

        // 确定下一个形态
        ShinGetterForm next = current switch
        {
            ShinGetterForm.Getter1 => ShinGetterForm.Getter2,
            ShinGetterForm.Getter2 => ShinGetterForm.Getter3,
            _ => ShinGetterForm.Getter1,
        };

        await TransformTo(choiceContext, player, next, cardSource);
    }

    public static async Task TransformTo(
        PlayerChoiceContext choiceContext,
        Player player,
        ShinGetterForm next,
        CardModel? cardSource)
    {
        if (!IsShinGetterPlayer(player))
        {
            return;
        }

        var creature = player.Creature;
        ShinGetterCardFramePatch.BeginFormTransition(next);
        try
        {
            if (creature.GetPower<SGP_ShinForm>() is { } shinForm)
                await PowerCmd.Remove(shinForm);
            if (next != ShinGetterForm.Getter1 && creature.GetPower<SGP_ShinGetterOne>() is { } one)
                await PowerCmd.Remove(one);
            if (next != ShinGetterForm.Getter2 && creature.GetPower<SGP_ShinGetterTwo>() is { } two)
                await PowerCmd.Remove(two);
            if (next != ShinGetterForm.Getter3 && creature.GetPower<SGP_ShinGetterThree>() is { } three)
                await PowerCmd.Remove(three);

            bool alreadyInTargetForm = next switch
            {
                ShinGetterForm.Getter1 => creature.GetPower<SGP_ShinGetterOne>() != null,
                ShinGetterForm.Getter2 => creature.GetPower<SGP_ShinGetterTwo>() != null,
                ShinGetterForm.Getter3 => creature.GetPower<SGP_ShinGetterThree>() != null,
                _ => true,
            };

            if (!alreadyInTargetForm)
                await ApplyFormPower(choiceContext, creature, next, player, cardSource);
        }
        finally
        {
            ShinGetterCardFramePatch.EndFormTransitionAndRefresh();
        }

        await NotifyTransform(creature);
    }

    private static async Task NotifyTransform(Creature creature)
    {
        var chosenOne = creature.GetPower<SGP_ChosenOne>();
        if (chosenOne != null)
            await chosenOne.OnTransform(creature);

        var battleInstinct = creature.Player?.GetRelic<SGR_BattleInstinct>();
        if (battleInstinct != null)
            await battleInstinct.OnTransform(creature);
    }

    /// <summary>
    /// 真化形态下的变形：不切换形态，但触发三个形态的出场效果。
    /// </summary>
    private static async Task TriggerShinFormTransform(PlayerChoiceContext choiceContext, Creature creature, CardModel? cardSource)
    {
        // 真化形态只触发一次“发生了变形”的事件，不切换或重复施加形态入场效果。
        await NotifyTransform(creature);
    }

    private static async Task ApplyFormPower(PlayerChoiceContext choiceContext, Creature creature, ShinGetterForm form, Player player, CardModel? cardSource)
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

    private static bool IsShinGetterPlayer(Player player) =>
        player.Character is ShinGetter;
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
