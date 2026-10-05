// Managed test bindings ONLY. Production Session/Plan/Catalog/DTO sources are linked
// verbatim. No Godot process/native runtime; all files live in a new temp directory.
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Godot
{
    public static class ProjectSettings { public static string GlobalizePath(string path) => path; }
    public static class GD { public static void PushWarning(string message) { } }
}
namespace MegaCrit.Sts2.Core.Localization
{
    public sealed class LocManager { public static LocManager Instance { get; } = new(); public string Language { get; set; } = "zhs"; }
}
namespace MegaCrit.Sts2.Core.Helpers { public static class NonInteractiveMode { public static bool IsActive { get; set; } } }
namespace MegaCrit.Sts2.Core.TestSupport { public static class TestMode { public static bool IsOn { get; set; } } }
namespace ShinGetterMod.Models.Characters
{
    public sealed class ShinGetter { public ModelId Id { get; } = new("CHARACTER", "SHIN_GETTER"); }
}
namespace MegaCrit.Sts2.Core.Models
{
    public sealed record ModelId(string Category, string Entry)
    {
        public static ModelId none { get; } = new("", "");
        public static string SlugifyCategory<T>() => "EVENT";
    }
    public static class ModelDb { public static T Character<T>() where T : new() => new(); }
    public sealed class EventModel { public ModelId Id { get; init; } = ModelId.none; public Player? Owner { get; init; } }
    public class Player { public object Character { get; set; } = new ShinGetterMod.Models.Characters.ShinGetter(); public RunState RunState { get; set; } = new(); }
}
namespace MegaCrit.Sts2.Core.Rooms
{
    public enum RoomType { Event, Combat }
    public class AbstractRoom { }
    public sealed class EventRoom : AbstractRoom { public ModelId ModelId { get; init; } = ModelId.none; }
}
namespace MegaCrit.Sts2.Core.Runs
{
    public enum GameMode { Standard, Daily }
    public sealed record MapCoord(int col, int row);
    public sealed class MapRoom { public RoomType RoomType { get; set; } = RoomType.Event; public ModelId ModelId { get; set; } = ModelId.none; }
    public sealed class MapPoint { public List<MapRoom> Rooms { get; set; } = new(); }
    public sealed class RunRng { public string Seed { get; } = "fixture"; }
    public sealed class RunState
    {
        public List<Player> Players { get; } = new(); public GameMode GameMode { get; set; }
        public List<string> Modifiers { get; } = new(); public int CurrentActIndex { get; set; }
        public List<List<MapPoint>> MapPointHistory { get; } = new(); public AbstractRoom? CurrentRoom { get; set; }
        public AbstractRoom? BaseRoom { get; set; } public MapCoord? CurrentMapCoord { get; set; } = new(0, 0);
        public RunRng Rng { get; } = new();
    }
    public sealed class HistoryPlayer { public ModelId Character { get; set; } = new("CHARACTER", "SHIN_GETTER"); }
    public sealed class RunHistory
    {
        public long StartTime { get; set; } public GameMode GameMode { get; set; } public List<string> Modifiers { get; } = new();
        public List<HistoryPlayer> Players { get; } = new() { new() }; public bool Win { get; set; } public bool WasAbandoned { get; set; }
        public ModelId KilledByEncounter { get; set; } = ModelId.none; public ModelId KilledByEvent { get; set; } = ModelId.none;
        public List<List<MapPoint>> MapPointHistory { get; } = new();
    }
    public sealed class RunManager
    {
        // Exact field name exercised by the production Harmony FieldRef.
        private long _startTime = 1000;
        public static RunManager Instance { get; } = new(); public bool IsInProgress { get; set; } = true;
        public bool ShouldSave { get; set; } = true; public DateTimeOffset? DailyTime { get; set; }
        public RunState? State { get; set; }
        public RunState? DebugOnlyGetState() => State;
        public void SetStart(long start) => _startTime = start;
    }
}
namespace MegaCrit.Sts2.Core.Entities.Players { public sealed class Player : MegaCrit.Sts2.Core.Models.Player { } }
namespace ShinGetterMod.Nodes.Events { internal static class NShinGetterBondDialogue { internal static bool IsOpen { get; set; } } }
namespace MegaCrit.Sts2.Core.DevConsole
{
    public sealed record CmdResult(bool success, string msg);
    public sealed class CompletionResult { public List<string> Candidates { get; init; } = new(); }
}
namespace MegaCrit.Sts2.Core.DevConsole.ConsoleCommands
{
    public abstract class AbstractConsoleCmd
    {
        public abstract string CmdName { get; } public abstract string Args { get; } public abstract string Description { get; }
        public abstract bool IsNetworked { get; } public virtual bool DebugOnly => true;
        public abstract MegaCrit.Sts2.Core.DevConsole.CmdResult Process(MegaCrit.Sts2.Core.Entities.Players.Player? player, string[] args);
        public virtual MegaCrit.Sts2.Core.DevConsole.CompletionResult GetArgumentCompletions(MegaCrit.Sts2.Core.Entities.Players.Player? player, string[] args) => new();
        protected MegaCrit.Sts2.Core.DevConsole.CompletionResult CompleteArgument(IEnumerable<string> candidates, string[] completedArgs, string partial) =>
            new() { Candidates = candidates.Where(c => c.StartsWith(partial, StringComparison.OrdinalIgnoreCase)).ToList() };
    }
}
namespace MegaCrit.Sts2.Core.Saves
{
    public enum ReadSaveStatus { Success, Error }
    public sealed record HistoryResult(ReadSaveStatus Status, RunHistory? SaveData);
    public sealed class SaveManager
    {
        public static SaveManager Instance { get; } = new(); public int CurrentProfileId { get; set; }
        public string Root { get; set; } = ""; public int HistoryReads { get; private set; }
        public List<RunHistory> Histories { get; } = new();
        public string GetProfileScopedPath(string name) => Path.Combine(Root, "profile-" + CurrentProfileId, name);
        public IEnumerable<string> GetAllRunHistoryNames() { HistoryReads++; return Enumerable.Range(0, Histories.Count).Select(i => i.ToString()); }
        public HistoryResult LoadRunHistory(string file) => new(ReadSaveStatus.Success, Histories[int.Parse(file)]);
    }
}
