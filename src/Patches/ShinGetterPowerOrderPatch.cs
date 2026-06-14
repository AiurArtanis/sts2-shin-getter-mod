using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NPowerContainer), "UpdatePositions")]
internal static class ShinGetterPowerOrderPatch
{
    private static void Prefix(List<NPower> ____powerNodes)
    {
        int formIndex = ____powerNodes.FindIndex(node => node.Model is
            SGP_ShinGetterOne or SGP_ShinGetterTwo or SGP_ShinGetterThree or SGP_ShinForm);
        if (formIndex <= 0)
            return;

        NPower formNode = ____powerNodes[formIndex];
        ____powerNodes.RemoveAt(formIndex);
        ____powerNodes.Insert(0, formNode);
    }
}
