using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Services;

// Dynamic types simulate assembly/type identity only; this does not execute BS2 or Godot.
internal static class Bs2CompatibilityChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
        var id = new ModelId("MODIFIER", "ST2BS-GIRLS_NIGHTMARE_END_STATS_MODIFIER");
        const string name = "St2bs.St2bsCode.Modifiers.GirlsNightmareEndStatsModifier";
        ModifierModel Marker(string assembly, string typeName, bool sealedType = true)
        {
            var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assembly), AssemblyBuilderAccess.Run).DefineDynamicModule(assembly);
            var type = module.DefineType(typeName, TypeAttributes.Public | (sealedType ? TypeAttributes.Sealed : 0), typeof(ModifierModel)).CreateType()!;
            var model = (ModifierModel)Activator.CreateInstance(type)!;
            model.Id = id;
            return model;
        }
        var unknown = new ModifierModel { Id = new("MODIFIER", "UNKNOWN_CHALLENGE") };
        Check(ShinGetterBondRunCompatibility.IsStandardModifiers(Array.Empty<ModifierModel>()), "vanilla empty modifiers changed");
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { unknown }), "unknown modifier accepted without BS2");
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { new ModifierModel { Id = id } }), "spoofed ID accepted without BS2");
        Check(!ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(new[] { new SerializableModifier { Id = id } }), "orphan history accepted without BS2");
        var marker = Marker("St2bs", name);
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker }), "unregistered marker accepted");
        ModelDb.Modifiers[id] = marker;
        Check(ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker }), "BS2 passive standard marker blocked");
        Check(ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(new[] { new SerializableModifier { Id = id } }), "BS2 saved marker blocked");
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker, unknown }), "mixed challenge accepted");
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker, marker }), "duplicate marker accepted");
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { Marker("Impostor", name) }), "wrong assembly accepted");
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { Marker("St2bs", name + "Other") }), "wrong full type accepted");
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { Marker("St2bs", name, false) }), "nonsealed type accepted");
        var wrongId = Marker("St2bs", name); wrongId.Id = unknown.Id;
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { wrongId }), "wrong ID accepted");
        Check(!ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(new[] { new SerializableModifier { Id = id }, new SerializableModifier { Id = unknown.Id } }), "mixed history accepted");
        Check(!ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(new[] { new SerializableModifier() }), "null history ID accepted");
        Check(!ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(new[] { new SerializableModifier { Id = unknown.Id } }), "unknown history ID accepted");
        ModelDb.FailModifierLookup = true;
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker }), "live lookup exception escaped or allowed");
        Check(!ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(new[] { new SerializableModifier { Id = id } }), "history lookup exception escaped or allowed");
        ModelDb.FailModifierLookup = false;
        Check(ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker }), "lookup recovery was negatively cached");
        // Model registration and saved properties are restored before a cold run.
        var coldMarker = (ModifierModel)Activator.CreateInstance(marker.GetType())!; coldMarker.Id = id;
        Check(ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { coldMarker }), "cold mutable instance rejected");
        var player = new Player(); player.RunState.Players.Add(player); player.RunState.Modifiers.Add(marker);
        var model = new EventModel { Id = new("EVENT", "THE_ARCHITECT"), Owner = player };
        var manager = RunManager.Instance; manager.IsInProgress = manager.ShouldSave = true; manager.DailyTime = null;
        Check(ShinGetterBondSession.IsEligible(model), "standard BS2 Architect rejected");
        player.RunState.Modifiers.Add(unknown);
        Check(!ShinGetterBondSession.IsEligible(model), "mixed BS2 Architect accepted"); player.RunState.Modifiers.Remove(unknown);
        player.RunState.GameMode = GameMode.Daily;
        Check(!ShinGetterBondSession.IsEligible(model), "daily accepted with stats marker"); player.RunState.GameMode = GameMode.Standard;
        manager.ShouldSave = false;
        Check(!ShinGetterBondSession.IsEligible(model), "unsaved accepted with stats marker"); manager.ShouldSave = true;
        player.RunState.Players.Add(new());
        Check(!ShinGetterBondSession.IsEligible(model), "multiplayer accepted with stats marker"); player.RunState.Players.RemoveAt(1);
        manager.DailyTime = DateTimeOffset.UtcNow;
        Check(!ShinGetterBondSession.IsEligible(model), "dated daily accepted with stats marker"); manager.DailyTime = null;
        player.Character = new object();
        Check(!ShinGetterBondSession.IsEligible(model), "other character accepted with stats marker"); player.Character = new ShinGetterMod.Models.Characters.ShinGetter();
        MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn = true;
        Check(!ShinGetterBondSession.IsEligible(model), "test mode accepted with stats marker"); MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn = false;
        MegaCrit.Sts2.Core.Helpers.NonInteractiveMode.IsActive = true;
        Check(!ShinGetterBondSession.IsEligible(model), "replay accepted with stats marker"); MegaCrit.Sts2.Core.Helpers.NonInteractiveMode.IsActive = false;
        manager.State = player.RunState;
        var consolePlayer = new MegaCrit.Sts2.Core.Entities.Players.Player { RunState = player.RunState };
        var cmd = new ShinGetterMod.Diagnostics.ShinGetterDialogueConsoleCmd();
        Check(cmd.Process(consolePlayer, new[] { "ALL", "clear" }).success, "passive marker console guard disagrees");
        player.RunState.Modifiers.Add(unknown);
        Check(!cmd.Process(consolePlayer, new[] { "ALL", "clear" }).success, "mixed console edit accepted");
        var plainHistory = new RunHistory { StartTime = 1 };
        var statsHistory = new RunHistory { StartTime = 2 }; statsHistory.Modifiers.Add(new() { Id = id });
        var mixedHistory = new RunHistory { StartTime = 3 }; mixedHistory.Modifiers.Add(new() { Id = id }); mixedHistory.Modifiers.Add(new() { Id = unknown.Id });
        SaveManager.Instance.Histories.Clear(); SaveManager.Instance.Histories.AddRange(new[] { plainHistory, statsHistory, mixedHistory });
        var session = new ShinGetterBondSession(model);
        var histories = (List<RunHistory>)typeof(ShinGetterBondSession).GetMethod("ReadReliableHistories", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(session, null)!;
        Check(histories.Select(h => h.StartTime).SequenceEqual(new long[] { 2, 1 }), "history standard/passive/mixed filters disagree");
        ModelDb.Modifiers.Clear();
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker }), "absent registry retained cached acceptance");
        ModelDb.Modifiers[id] = new ModifierModel { Id = id };
        Check(!ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { marker }), "fake canonical ID trusted");
        Check(!ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(new[] { new SerializableModifier { Id = id } }), "fake canonical history trusted");
        ModelDb.Modifiers[id] = marker;
        Check(ShinGetterBondRunCompatibility.IsStandardModifiers(new[] { coldMarker }), "late registration stayed rejected");
        return checks;
    }
}
