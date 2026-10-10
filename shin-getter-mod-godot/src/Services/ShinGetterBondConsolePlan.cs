#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ShinGetterMod.Services;

/// <summary>Validated, deterministic console edit. No gameplay RNG or run-history writes.</summary>
internal sealed class ShinGetterBondConsolePlan
{
    internal const string ChoicesToken = "@choices";
    internal const string Usage = "sgd <NPC|ALL> status|clear\n"
        + "sgd <NPC|ALL> unlock <RYOMA|HAYATO|BENKEI|ALL> <completed:0-3>\n"
        + "sgd <NPC|ALL> unlock <first|win|loss|return|chat> [variant:1-N]";
    internal static readonly string[] Npcs = ShinGetterDialogueCatalog.BondNpcs.Concat(new[] { "NEOW", "THE_ARCHITECT" }).ToArray();
    internal static readonly string[] Operations = { "status", "clear", "unlock" };
    internal static readonly string[] UnlockKinds = { "RYOMA", "HAYATO", "BENKEI", "ALL", "first", "win", "loss", "return", "chat" };
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["欧洛巴斯"] = "OROBAS", ["坦克斯"] = "TANX", ["瓦库"] = "VAKUU", ["达弗"] = "DARV",
        ["佩尔"] = "PAEL", ["特兹卡塔拉"] = "TEZCATARA", ["诺奴佩普"] = "NONUPEIPE",
        ["涅奥"] = "NEOW", ["建筑师"] = "THE_ARCHITECT", ["Architect"] = "THE_ARCHITECT",
        ["龙马"] = "RYOMA", ["流龙马"] = "RYOMA", ["隼人"] = "HAYATO", ["神隼人"] = "HAYATO",
        ["弁庆"] = "BENKEI", ["车弁庆"] = "BENKEI",
        ["查询"] = "STATUS", ["清空"] = "CLEAR", ["解锁"] = "UNLOCK",
        ["初见"] = "FIRST", ["战胜"] = "WIN", ["战败"] = "LOSS", ["久违"] = "RETURN", ["聊天"] = "CHAT",
    };
    internal string[] Targets { get; private init; } = Array.Empty<string>();
    private string _operation = "";
    private string _driver = "";
    private int _completed;
    private Dictionary<string, string> _dialogues = new(StringComparer.Ordinal);
    internal bool IsReadOnly => _operation == "STATUS";

    internal static string Canonical(string token) => Aliases.TryGetValue(token, out string? canonical) ? canonical : token.ToUpperInvariant();

    internal static string[] NumberCompletions(string npcToken, string kindToken)
    {
        string kind = Canonical(kindToken);
        if (kind == "ALL" || ShinGetterDialogueCatalog.Drivers.Contains(kind)) return new[] { "0", "1", "2", "3" };
        string npc = Canonical(npcToken);
        var counts = (npc == "ALL" ? Npcs : new[] { npc }).Select(target => ShinGetterDialogueCatalog.All
            .Count(d => d.Npc == target && d.Id.StartsWith(target + "_" + kind + "_", StringComparison.Ordinal))).Where(n => n > 0).ToArray();
        return counts.Length == 0 ? Array.Empty<string>() : Enumerable.Range(1, counts.Min()).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray();
    }

    internal static bool TryParse(string[] args, out ShinGetterBondConsolePlan plan, out string error)
    {
        plan = new();
        error = Usage;
        if (args.Length < 2) return false;
        string npc = Canonical(args[0]);
        if (npc != "ALL" && !Npcs.Contains(npc)) { error = "Unknown NPC. Use: " + string.Join(", ", Npcs) + ", ALL"; return false; }
        string[] targets = npc == "ALL" ? Npcs.ToArray() : new[] { npc };
        plan = new() { Targets = targets, _operation = Canonical(args[1]) };
        if (plan._operation is "STATUS" or "CLEAR") return args.Length == 2;
        if (plan._operation != "UNLOCK" || args.Length is < 3 or > 4) return false;
        string kind = Canonical(args[2]);
        if (kind == "ALL" || ShinGetterDialogueCatalog.Drivers.Contains(kind))
        {
            if (args.Length != 4 || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                || number is < 0 or > 3) { error = "Completed bond stages must be an integer from 0 to 3.\n" + Usage; return false; }
            targets = targets.Where(ShinGetterDialogueCatalog.BondNpcs.Contains).ToArray();
            if (targets.Length == 0) { error = "Neow and the Architect have no pilot bond stages."; return false; }
            plan = new() { Targets = targets, _operation = "UNLOCK", _driver = kind, _completed = number };
            return true;
        }
        if (kind is not ("FIRST" or "WIN" or "LOSS" or "RETURN" or "CHAT")) return false;
        int variant = 1;
        if (args.Length == 4 && (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out variant) || variant < 1)) return false;
        var dialogues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string target in targets)
        {
            string[] candidates = ShinGetterDialogueCatalog.All.Where(d => d.Npc == target && d.Id.StartsWith(target + "_" + kind + "_", StringComparison.Ordinal))
                .OrderBy(d => d.Id, StringComparer.Ordinal).Select(d => d.Id).ToArray();
            if (candidates.Length == 0 && npc == "ALL") continue; // ALL means every applicable NPC (Neow has no return).
            if (variant > candidates.Length)
            {
                error = $"{target} has {candidates.Length} {kind.ToLowerInvariant()} variants; nothing was changed.";
                return false;
            }
            dialogues[target] = candidates[variant - 1];
        }
        plan = new() { Targets = dialogues.Keys.ToArray(), _operation = "UNLOCK", _dialogues = dialogues };
        return plan.Targets.Length != 0;
    }

    internal void Apply(ShinGetterBondSave save)
    {
        if (IsReadOnly) throw new InvalidOperationException("Status is read-only.");
        foreach (string npc in Targets)
        {
            // Invalidate only this NPC's old snapshots; never touch current event rewards.
            foreach (string key in save.Encounters.Where(p => p.Value.Npc == npc).Select(p => p.Key).ToArray()) save.Encounters.Remove(key);
            save.DebugNextDialogues.Remove(npc);
            if (_operation == "CLEAR" || (_dialogues.TryGetValue(npc, out string? selected) && selected == npc + "_FIRST_01"))
            {
                var ids = ShinGetterDialogueCatalog.All.Where(d => d.Npc == npc).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
                save.Completed.RemoveWhere(ids.Contains);
                save.LegacyAcquaintances.Remove(npc);
                save.LastMetRun.Remove(npc);
                save.RespondedResults.Remove(npc);
                if (_operation != "CLEAR") save.DebugNextDialogues[npc] = npc + "_FIRST_01";
                continue;
            }
            save.Completed.Add(npc + "_FIRST_01");
            if (_driver.Length != 0)
            {
                foreach (string driver in _driver == "ALL" ? ShinGetterDialogueCatalog.Drivers : new[] { _driver })
                    SetStages(save, npc, driver, _completed);
                // At 3 no fourth segment exists. Show the remaining pilots (or chat).
                save.DebugNextDialogues[npc] = _driver != "ALL" && _completed < 3
                    ? $"{npc}_{_driver}_BOND_{_completed + 1:00}"
                    : HasUnfinished(save, npc) ? ChoicesToken : npc + "_CHAT_01";
            }
            else
            {
                // Familiar outcome text presumes all nine segments; set prerequisites,
                // but simulate only a dialogue request, never a real win/death history.
                if (ShinGetterDialogueCatalog.BondNpcs.Contains(npc))
                    foreach (string driver in ShinGetterDialogueCatalog.Drivers) SetStages(save, npc, driver, 3);
                save.DebugNextDialogues[npc] = _dialogues[npc];
            }
        }
        ValidatePending(save);
    }

    private static void SetStages(ShinGetterBondSave save, string npc, string driver, int count)
    {
        for (int stage = 1; stage <= 3; stage++)
        {
            string id = $"{npc}_{driver}_BOND_{stage:00}";
            if (stage <= count) save.Completed.Add(id); else save.Completed.Remove(id);
        }
    }

    private static bool HasUnfinished(ShinGetterBondSave save, string npc) => ShinGetterDialogueCatalog.Drivers
        .Any(driver => !save.Completed.Contains($"{npc}_{driver}_BOND_03"));

    internal static void ValidatePending(ShinGetterBondSave save)
    {
        foreach (var (npc, id) in save.DebugNextDialogues)
        {
            if (!Npcs.Contains(npc) || string.IsNullOrEmpty(id)) throw new InvalidDataException("Invalid pending test conversation; original file retained.");
            if (id == ChoicesToken)
            {
                if (!ShinGetterDialogueCatalog.BondNpcs.Contains(npc) || !HasUnfinished(save, npc)
                    || !save.Completed.Contains(npc + "_FIRST_01"))
                    throw new InvalidDataException("Invalid pending pilot selection; original file retained.");
            }
            else
            {
                var dialogue = ShinGetterDialogueCatalog.All.FirstOrDefault(d => d.Npc == npc && d.Id == id)
                    ?? throw new InvalidDataException("Pending test conversation belongs to another NPC; original file retained.");
                if (dialogue.Stage > 0 && (!save.Completed.Contains(npc + "_FIRST_01") || save.Completed.Contains(id)
                    || Enumerable.Range(1, dialogue.Stage - 1).Any(stage => !save.Completed.Contains($"{npc}_{dialogue.Driver}_BOND_{stage:00}"))))
                    throw new InvalidDataException("Pending test conversation has invalid prerequisites; original file retained.");
            }
        }
    }

    internal string Describe(ShinGetterBondSave save) => string.Join("\n", Targets.Select(npc =>
    {
        bool first = save.Completed.Contains(npc + "_FIRST_01");
        string stages = ShinGetterDialogueCatalog.BondNpcs.Contains(npc) ? "; " + string.Join(", ", ShinGetterDialogueCatalog.Drivers.Select(driver =>
            driver + "=" + Enumerable.Range(1, 3).Count(n => save.Completed.Contains($"{npc}_{driver}_BOND_{n:00}")))) : "; no pilot bonds";
        return $"{npc}: first={first}{stages}; next={save.DebugNextDialogues.GetValueOrDefault(npc, "normal")}";
    })) + (IsReadOnly ? "" : "\nSaved. Next eligible encounter uses this request once; gameplay history and rewards are unchanged.");
}
