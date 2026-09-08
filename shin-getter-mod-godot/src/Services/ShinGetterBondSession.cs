#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Services;

internal sealed class ShinGetterBondSave
{
    public ShinGetterBondSave() { }
    public int Schema { get; set; } = 1;
    public long Revision { get; set; }
    public HashSet<string> Completed { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> LastMetRun { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> RespondedResults { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ShinGetterBondEncounter> Encounters { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ShinGetterBondEncounter
{
    public ShinGetterBondEncounter() { }
    public string Npc { get; set; } = "";
    public long Run { get; set; }
    public string DialogueId { get; set; } = "";
    public string TextVersion { get; set; } = "";
    public int Line { get; set; }
    public bool Closed { get; set; }
    public bool Completed { get; set; }
    public bool WasShown { get; set; }
    public long ResultRun { get; set; }
    public HashSet<int> ConsumedCues { get; set; } = new();
    // Freeze every translation too: updating the mod during a saved conversation must
    // not silently change its content or line count on resume/language switch.
    public Dictionary<string, string[]> LinesByLanguage { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// One scene-owned transaction controller, never a static Player/Node reference.
/// The sidecar belongs to the active account/profile, not global mod configuration.
/// Every write is copy-on-write: the UI can advance only after the atomic rename.
/// </summary>
internal sealed class ShinGetterBondSession
{
    private static readonly AccessTools.FieldRef<RunManager, long> StartTime = AccessTools.FieldRefAccess<RunManager, long>("_startTime");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly int _profile;
    private readonly string _key;
    private readonly string _npc;
    private readonly long _run;
    private ShinGetterBondSave? _save;
    private ShinGetterBondEncounter? _pendingEncounter;
    private bool _locallySkipped;
    internal string Error { get; private set; } = "";
    internal ShinGetterBondEncounter? Encounter => _save?.Encounters.GetValueOrDefault(_key);
    internal bool Closed => _locallySkipped || Encounter?.Closed == true;
    internal string[] CurrentLines(string language) => Encounter!.LinesByLanguage[
        language is "zhs" or "jpn" ? language : "eng"];
    internal IReadOnlyList<string> Choices => ShinGetterDialogueCatalog.Drivers
        .Select(driver => NextFor(driver)).Where(id => id != null).Cast<string>().ToArray();

    internal static bool IsEligible(EventModel model) => model.Owner is { Character: ShinGetter } owner
        && owner.RunState.Players.Count == 1
        && owner.RunState.GameMode == GameMode.Standard
        && owner.RunState.Modifiers.Count == 0
        && RunManager.Instance.IsInProgress && RunManager.Instance.ShouldSave
        && !RunManager.Instance.DailyTime.HasValue && !TestMode.IsOn && !NonInteractiveMode.IsActive
        && ShinGetterDialogueCatalog.ContainsNpc(model.Id.Entry);

    internal ShinGetterBondSession(EventModel model)
    {
        _profile = SaveManager.Instance.CurrentProfileId;
        _path = ProjectSettings.GlobalizePath(SaveManager.Instance.GetProfileScopedPath("shin_getter_bonds.json"));
        _run = StartTime(RunManager.Instance);
        _npc = model.Id.Entry;
        _key = $"{_run}:{model.Owner!.RunState.Rng.Seed}:{model.Owner.RunState.RunLocation}:{_npc}";
    }

    internal bool Begin()
    {
        try
        {
            if (_save == null) _save = Read();
            if (Encounter != null) return ValidateEncounter(Encounter);
            // Keep the draw across a failed write/retry. Never draw on gameplay RNG.
            _pendingEncounter ??= SelectEncounter();
            return Commit(next =>
            {
                foreach (string key in next.Encounters.Where(pair => pair.Value.Run < _run).Select(pair => pair.Key).ToArray())
                    next.Encounters.Remove(key);
                next.Encounters[_key] = _pendingEncounter!;
            });
        }
        catch (Exception ex) { Error = ex.Message; return false; }
    }

    internal bool MarkDisplayed()
    {
        if (Encounter == null) return false;
        if (Encounter.WasShown) return true;
        return Commit(next =>
        {
            next.Encounters[_key].WasShown = true;
            next.LastMetRun[_npc] = _run;
        });
    }

    private bool ValidateEncounter(ShinGetterBondEncounter encounter)
    {
        if (encounter.Closed || encounter.DialogueId.Length == 0) return true;
        if (!ShinGetterDialogueCatalog.All.Any(d => d.Id == encounter.DialogueId && d.Npc == _npc)
            || encounter.LinesByLanguage == null || string.IsNullOrEmpty(encounter.TextVersion)
            || !encounter.LinesByLanguage.TryGetValue("zhs", out var source) || source.Length == 0
            || encounter.Line < 0 || encounter.Line >= source.Length
            || new[] { "zhs", "eng", "jpn" }.Any(language => !encounter.LinesByLanguage.TryGetValue(language, out var lines)
                || lines == null || lines.Length != source.Length || lines.Any(string.IsNullOrWhiteSpace)))
        {
            Error = "The saved conversation snapshot is incomplete. Skip this encounter to keep existing progress.";
            return false;
        }
        return true;
    }

    private string? NextFor(string driver)
    {
        if (_save == null || !ShinGetterDialogueCatalog.BondNpcs.Contains(_npc)) return null;
        for (int stage = 1; stage <= 3; stage++)
        {
            string id = $"{_npc}_{driver}_BOND_{stage:00}";
            if (!_save.Completed.Contains(id)) return id;
        }
        return null;
    }

    private ShinGetterBondEncounter SelectEncounter()
    {
        var encounter = new ShinGetterBondEncounter { Npc = _npc, Run = _run };
        string first = _npc + "_FIRST_01";
        if (!_save!.Completed.Contains(first)) return WithDialogue(encounter, first);
        if (Choices.Count != 0) return encounter;

        var histories = ReadReliableHistories();
        long lastMet = _save.LastMetRun.GetValueOrDefault(_npc);
        // A real encounter in the current run clears the gap, even after skip.
        bool longAbsence = _npc != "NEOW" && lastMet > 0 && lastMet != _run
            && histories.Count(h => h.StartTime > lastMet && h.StartTime < _run) >= 5;
        if (longAbsence) return WithDialogue(encounter, _npc + "_RETURN_01");

        RunHistory? latest = histories.FirstOrDefault();
        if (latest != null && latest.StartTime != _save.RespondedResults.GetValueOrDefault(_npc)
            && RandomNumberGenerator.GetInt32(2) == 0)
        {
            // Abandonment is stored distinctly; never present a death story for it.
            bool death = !latest.Win && !latest.WasAbandoned
                && (latest.KilledByEncounter != ModelId.none || latest.KilledByEvent != ModelId.none);
            if (latest.Win || death)
            {
                string prefix = _npc + (latest.Win ? "_WIN_" : "_LOSS_");
                encounter.ResultRun = latest.StartTime;
                return WithDialogue(encounter, Draw(ShinGetterDialogueCatalog.All.Where(d => d.Id.StartsWith(prefix, StringComparison.Ordinal)).Select(d => d.Id)));
            }
        }
        var candidates = ShinGetterDialogueCatalog.All.Where(d => d.Id.StartsWith(_npc + "_CHAT_", StringComparison.Ordinal)).Select(d => d.Id).ToList();
        if (_npc == "THE_ARCHITECT")
        {
            foreach (var (source, echo) in new[]
            {
                ("VAKUU_RYOMA_BOND_03", "THE_ARCHITECT_STORY_VAKUU_01"),
                ("PAEL_BENKEI_BOND_03", "THE_ARCHITECT_STORY_PAEL_01"),
                ("OROBAS_RYOMA_BOND_03", "THE_ARCHITECT_STORY_OROBAS_01"),
            })
                if (_save.Completed.Contains(source)) candidates.Add(echo);
        }
        return WithDialogue(encounter, Draw(candidates));
    }

    private List<RunHistory> ReadReliableHistories()
    {
        var histories = new List<RunHistory>();
        foreach (string file in SaveManager.Instance.GetAllRunHistoryNames())
        {
            var result = SaveManager.Instance.LoadRunHistory(file);
            // Repaired/data-loss history is not proof of an outcome or a gap.
            if (result.Status != ReadSaveStatus.Success || result.SaveData is not { } history) continue;
            if (history.StartTime >= _run || history.GameMode != GameMode.Standard || history.Modifiers.Count != 0
                || history.Players.Count != 1 || history.Players[0].Character.Entry != "SHIN_GETTER") continue;
            histories.Add(history);
        }
        return histories.GroupBy(h => h.StartTime).Select(g => g.First()).OrderByDescending(h => h.StartTime).ToList();
    }

    private static string Draw(IEnumerable<string> candidates)
    {
        var ids = candidates.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        return ids[RandomNumberGenerator.GetInt32(ids.Length)];
    }

    private static ShinGetterBondEncounter WithDialogue(ShinGetterBondEncounter encounter, string id)
    {
        encounter.DialogueId = id;
        encounter.TextVersion = ShinGetterDialogueCatalog.Get(id).Version;
        encounter.LinesByLanguage = new[] { "zhs", "eng", "jpn" }.ToDictionary(
            language => language, language => ShinGetterDialogueCatalog.Localize(id, language).Lines.ToArray(), StringComparer.Ordinal);
        encounter.Line = 0;
        return encounter;
    }

    internal bool Select(string id)
    {
        if (Closed || Encounter == null || Encounter.DialogueId.Length != 0 || !Choices.Contains(id)) return false;
        return Commit(next => WithDialogue(next.Encounters[_key], id));
    }

    internal bool Advance()
    {
        if (Closed || Encounter == null || Encounter.DialogueId.Length == 0) return false;
        if (Encounter.Line + 1 < CurrentLines("zhs").Length)
            return Commit(next => next.Encounters[_key].Line++);
        return Commit(next =>
        {
            var encounter = next.Encounters[_key];
            // Only the explicit confirmation after the final visible line reaches here.
            next.Completed.Add(encounter.DialogueId);
            if (encounter.ResultRun > 0) next.RespondedResults[_npc] = encounter.ResultRun;
            encounter.Completed = true;
            encounter.Closed = true;
        });
    }

    internal bool ConsumeCue(int line)
    {
        if (Encounter == null || Encounter.ConsumedCues.Contains(line)) return false;
        return Commit(next => next.Encounters[_key].ConsumedCues.Add(line));
    }

    internal void Skip()
    {
        // Read failures must not overwrite the damaged file. The scene still unlocks.
        if (Encounter != null) Commit(next => next.Encounters[_key].Closed = true);
        _locallySkipped = true;
    }

    private ShinGetterBondSave Read()
    {
        if (!File.Exists(_path)) return new();
        var save = JsonSerializer.Deserialize<ShinGetterBondSave>(File.ReadAllText(_path), JsonOptions)
            ?? throw new InvalidDataException("Empty relationship save; original file retained.");
        if (save.Schema != 1 || save.Completed == null || save.Encounters == null
            || save.LastMetRun == null || save.RespondedResults == null)
            throw new InvalidDataException("Unsupported relationship save; original file retained.");
        foreach (string id in save.Completed)
        {
            var dialogue = ShinGetterDialogueCatalog.All.FirstOrDefault(d => d.Id == id)
                ?? throw new InvalidDataException("Unknown completed dialogue; original file retained.");
            if (dialogue.Stage > 0 && (!save.Completed.Contains(dialogue.Npc + "_FIRST_01")
                || Enumerable.Range(1, dialogue.Stage - 1).Any(stage => !save.Completed.Contains($"{dialogue.Npc}_{dialogue.Driver}_BOND_{stage:00}"))))
                throw new InvalidDataException("Non-contiguous relationship progress; original file retained.");
        }
        if (save.Encounters.Values.Any(e => e == null || e.ConsumedCues == null || e.DialogueId == null
            || e.Npc == null || e.TextVersion == null || e.Run <= 0)
            || save.LastMetRun.Values.Any(run => run <= 0) || save.RespondedResults.Values.Any(run => run <= 0))
            throw new InvalidDataException("Invalid relationship records; original file retained.");
        return save;
    }

    private bool Commit(Action<ShinGetterBondSave> change)
    {
        try
        {
            if (SaveManager.Instance.CurrentProfileId != _profile) throw new IOException("Active save profile changed.");
            if (_save == null) throw new IOException("Relationship save is not readable.");
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var transactionLock = new FileStream(_path + ".lock", FileMode.OpenOrCreate,
                System.IO.FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            if (Read().Revision != _save.Revision) throw new IOException("Relationship save changed in another conversation. Re-enter to reload.");
            var next = JsonSerializer.Deserialize<ShinGetterBondSave>(JsonSerializer.Serialize(_save, JsonOptions), JsonOptions)!;
            change(next);
            next.Revision++;
            string temporary = _path + ".pending";
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(next, JsonOptions);
            using (var file = new FileStream(temporary, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".backup");
            else File.Move(temporary, _path);
            _save = next;
            Error = "";
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            GD.PushWarning("Shin Getter relationship progress was not saved: " + ex.Message);
            return false;
        }
    }
}
