using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardModel), "get_HoverTips")]
internal static class ShinGetterCardHoverTipsPatch
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

    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not ShinGetterCardBase)
            return;

        string description = __instance.Description.GetRawText();
        IEnumerable<IHoverTip> contextualTips = TermTips
            .Where(pair => description.Contains(pair.Key, StringComparison.Ordinal))
            .Select(pair => pair.Value(__instance));
        __result = IHoverTip.RemoveDupes(__result.Concat(contextualTips));
    }

    private static IHoverTip CustomTip(string key) => new HoverTip(
        new LocString("static_hover_tips", key + ".title"),
        new LocString("static_hover_tips", key + ".description"));
}
