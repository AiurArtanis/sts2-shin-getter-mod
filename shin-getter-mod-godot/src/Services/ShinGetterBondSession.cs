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
using MegaCrit.Sts2.Core.Rooms;
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
    public int LegacyMigrationVersion { get; set; }
    // Evidence of an old encounter is not completion of a new story segment.
    public Dictionary<string, long> LegacyAcquaintances { get; set; } = new(StringComparer.Ordinal);
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
    private readonly string _identityError = "";
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
        _key = "";
        try { _key = BuildEncounterKey(model, _run); }
        catch (InvalidDataException ex) { _identityError = ex.Message; }
    }

    private static string BuildEncounterKey(EventModel model, long run)
    {
        var state = model.Owner!.RunState;
        int act = state.CurrentActIndex;
        if (run <= 0 || act < 0 || act >= state.MapPointHistory.Count)
            throw new InvalidDataException("No persistent map history for this conversation. Skip to continue.");
        var points = state.MapPointHistory[act];
        if (points.Count == 0 || state.CurrentRoom is not EventRoom current || current.ModelId != model.Id)
            throw new InvalidDataException("Conversation does not match the active event. Skip to continue.");
        var rooms = points[points.Count - 1].Rooms;
        // ToSave/FromSerializable preserve map history, including ordered Rooms.
        // A reconstructed base event always owns entry 0, even if later child rooms
        // remain in history. A new subroom is appended before EnterInternal.
        // AbstractRoom.Id and RunLocation.roomId are deliberately NOT persisted here.
        int room = ReferenceEquals(current, state.BaseRoom) ? 0
            : rooms.FindLastIndex(entry => entry.RoomType == RoomType.Event && entry.ModelId == model.Id);
        if (room < 0 || room >= rooms.Count || rooms[room].RoomType != RoomType.Event || rooms[room].ModelId != model.Id)
            throw new InvalidDataException("No reliable event history slot. Skip to continue.");
        string coord = state.CurrentMapCoord is { } mapCoord ? $"{mapCoord.col},{mapCoord.row}" : "none";
        return $"encounter-v2:{run}:{state.Rng.Seed}:{act}:{coord}:{points.Count - 1}:{room}:{model.Id}";
    }

    internal bool Begin()
    {
        try
        {
            if (_identityError.Length != 0) throw new InvalidDataException(_identityError);
            if (_save == null) _save = Read(migrateIfMissing: true);
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
        if (!IsAcquainted(_save!, _npc)) return WithDialogue(encounter, first);
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
            if (history.StartTime <= 0 || history.StartTime >= _run || history.GameMode != GameMode.Standard || history.Modifiers.Count != 0
                || history.Players.Count != 1 || history.Players[0].Character != ModelDb.Character<ShinGetter>().Id) continue;
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

    private static bool IsAcquainted(ShinGetterBondSave save, string npc) =>
        save.Completed.Contains(npc + "_FIRST_01") || save.LegacyAcquaintances.ContainsKey(npc);

    private ShinGetterBondSave CreateWithLegacyAcquaintances()
    {
        var save = new ShinGetterBondSave { LegacyMigrationVersion = 1 };
        // Only the first sidecar creation imports prior, successfully read standard
        // solo Shin Getter runs. Aggregate AncientStats cannot distinguish run modes.
        // Never import current-run visits, counts, role stages or result responses.
        try
        {
            var known = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var history in ReadReliableHistories())
            {
                if (history.MapPointHistory == null || history.MapPointHistory.Any(act => act == null
                    || act.Any(point => point == null || point.Rooms == null || point.Rooms.Any(room => room == null)))) continue;
                foreach (var room in history.MapPointHistory.SelectMany(act => act).SelectMany(point => point.Rooms))
                {
                    if (room.RoomType != RoomType.Event || room.ModelId is not { } id
                        || id.Category != ModelId.SlugifyCategory<EventModel>()
                        || !ShinGetterDialogueCatalog.ContainsNpc(id.Entry)) continue;
                    known[id.Entry] = Math.Max(known.GetValueOrDefault(id.Entry), history.StartTime);
                }
            }
            save.LegacyAcquaintances = known;
        }
        catch (Exception ex)
        {
            // No trustworthy evidence: keep initial meetings, do not guess unlocks.
            GD.PushWarning("Shin Getter legacy acquaintances were not imported: " + ex.Message);
        }
        return save;
    }

    private ShinGetterBondSave Read(bool migrateIfMissing = false)
    {
        if (!File.Exists(_path)) return migrateIfMissing ? CreateWithLegacyAcquaintances() : new();
        var save = JsonSerializer.Deserialize<ShinGetterBondSave>(File.ReadAllText(_path), JsonOptions)
            ?? throw new InvalidDataException("Empty relationship save; original file retained.");
        if (save.Schema != 1 || save.Completed == null || save.Encounters == null
            || save.LastMetRun == null || save.RespondedResults == null || save.LegacyAcquaintances == null
            || save.LegacyMigrationVersion is < 0 or > 1)
            throw new InvalidDataException("Unsupported relationship save; original file retained.");
        if (save.LegacyAcquaintances.Any(pair => !ShinGetterDialogueCatalog.ContainsNpc(pair.Key) || pair.Value <= 0))
            throw new InvalidDataException("Invalid legacy acquaintance evidence; original file retained.");
        // Unreleased roomId-based snapshots cannot be assigned a history slot safely.
        // Keep the original file and allow local skip instead of replaying a new story.
        if (save.Encounters.Any(pair => pair.Value != null && pair.Value.Run == _run
            && !pair.Key.StartsWith("encounter-v2:", StringComparison.Ordinal)))
            throw new InvalidDataException("Legacy encounter identity cannot be restored safely. Original file retained; skip this encounter.");
        foreach (string id in save.Completed)
        {
            var dialogue = ShinGetterDialogueCatalog.All.FirstOrDefault(d => d.Id == id)
                ?? throw new InvalidDataException("Unknown completed dialogue; original file retained.");
            if (dialogue.Stage > 0 && (!IsAcquainted(save, dialogue.Npc)
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
            // Existing sidecars are already authoritative: never re-import histories
            // to turn a skipped/incomplete initial meeting into an acquaintance.
            next.LegacyMigrationVersion = 1;
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
