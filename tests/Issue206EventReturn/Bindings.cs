// Managed environment stand-ins ONLY. The complete production bridge is linked
// unchanged, with actual Harmony FieldRef. This is not native Godot/input proof.
namespace Godot
{
    public static class GD { public static void PushWarning(string message) { } }
    public class GodotObject
    {
        public bool Valid { get; set; } = true;
        public static bool IsInstanceValid(GodotObject value) => value.Valid;
    }
    public class Control : GodotObject
    {
        public bool Visible { get; set; } = true;
        public bool InTree { get; set; } = true;
        public bool Queued { get; set; }
        public bool IsInsideTree() => InTree;
        public bool IsQueuedForDeletion() => Queued;
        public void Hide() => Visible = false;
    }
    public sealed class Callable(Action action)
    {
        public static List<Action> Deferred { get; } = new();
        public static Callable From(Action action) => new(action);
        public void CallDeferred() => Deferred.Add(action);
    }
}
namespace MegaCrit.Sts2.Core.Entities.Ancients
{
    public enum ArchitectAttackers { Player }
    public sealed class AncientDialogueLine(string sound)
    {
        public string GetSfxOrFallbackPath() => string.IsNullOrEmpty(sound) ? "event:/sfx/ui/enchant_simple" : sound;
    }
    public sealed class AncientDialogueSet
    {
        public List<MegaCrit.Sts2.Core.Events.AncientDialogue> Dialogues { get; } = new();
        public IEnumerable<MegaCrit.Sts2.Core.Events.AncientDialogue> GetAllDialogues() => Dialogues;
        public IEnumerable<MegaCrit.Sts2.Core.Events.AncientDialogue> GetValidDialogues(object character, int visits, int total, bool any) => Dialogues;
    }
}
namespace MegaCrit.Sts2.Core.Events
{
    public sealed class EventOption { public bool IsLocked { get; set; } }
    public sealed class AncientDialogue(string text) { public string Text { get; } = text; public List<MegaCrit.Sts2.Core.Entities.Ancients.AncientDialogueLine> Lines { get; } = new(); public MegaCrit.Sts2.Core.Entities.Ancients.ArchitectAttackers EndAttackers { get; set; } }
}
namespace MegaCrit.Sts2.Core.Localization { public sealed class LocString(string table, string key) { public string Table { get; } = table; public string Key { get; } = key; } }
namespace MegaCrit.Sts2.Core.Models
{
    public class EventModel
    {
        public object Id { get; } = new();
        public Player Owner { get; } = new();
        public Godot.Control? Node { get; set; }
        public bool Eligible { get; set; } = true;
        public bool IsFinished { get; set; }
        public MegaCrit.Sts2.Core.Events.EventOption[] CurrentOptions { get; private set; } = Array.Empty<MegaCrit.Sts2.Core.Events.EventOption>();
        protected void SetEventState(MegaCrit.Sts2.Core.Localization.LocString description, IEnumerable<MegaCrit.Sts2.Core.Events.EventOption> options) { CurrentOptions = options.ToArray(); }
    }
    public class AncientEventModel : EventModel
    {
        public IEnumerable<object> AnyCharacterDialogueBlacklist => Array.Empty<object>();
        public MegaCrit.Sts2.Core.Entities.Ancients.AncientDialogueSet DialogueSet { get; } = new();
    }
    public sealed class Player { public Character Character { get; } = new(); }
    public sealed class Character { public object Id { get; } = new(); }
}
namespace MegaCrit.Sts2.Core.Random
{
    public sealed class Rng
    {
        public static Rng Chaotic { get; } = new();
        public T NextItem<T>(IEnumerable<T> items) => items.First();
    }
}
namespace MegaCrit.Sts2.Core.Helpers
{
    public static class TaskHelper { public static void RunSafely(Task task) => task.GetAwaiter().GetResult(); }
}
namespace MegaCrit.Sts2.Core.Saves
{
    public sealed class SaveManager
    {
        public static SaveManager Instance { get; } = new();
        public Progress Progress { get; } = new();
    }
    public sealed class Progress { public Stats? GetStatsForAncient(object id) => null; }
    public sealed class Stats { public int GetVisitsAs(object id) => 0; public int TotalVisits => 0; }
}
namespace MegaCrit.Sts2.Core.Models.Events
{
    public sealed class TheArchitect : MegaCrit.Sts2.Core.Models.EventModel
    {
        private MegaCrit.Sts2.Core.Events.AncientDialogue? _dialogue;
        private int _currentLineIndex = 0;
        public int NativeLines { get; private set; }
        public bool FailLegacyLoad { get; set; }
        public MegaCrit.Sts2.Core.Events.AncientDialogue? StoredDialogue => _dialogue;
        public void LoadDialogue() { if (FailLegacyLoad) throw new IOException("injected legacy failure"); _dialogue = new("legacy"); _dialogue.Lines.Add(new("legacy")); }
        public Task PlayCurrentLine() { NativeLines++; _ = _currentLineIndex; return Task.CompletedTask; }
        private MegaCrit.Sts2.Core.Events.EventOption CreateProceedOption() => new();
    }
}
namespace MegaCrit.Sts2.Core.Nodes.GodotExtensions
{
    public static class FocusExtensions
    {
        public static void TryGrabFocus(this Godot.Control control) { }
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Events
{
    public class NEventOptionButton : Godot.Control
    {
        public MegaCrit.Sts2.Core.Events.EventOption Option { get; } = new();
        public bool IsEnabled { get; private set; } = true;
        public int EnableCalls { get; private set; }
        public bool MouseStops { get; set; }
        public void Disable() => IsEnabled = false;
        public void Enable() { if (!IsEnabled) { IsEnabled = true; EnableCalls++; } }
        public void EnableButton() => MouseStops = true; // official109 sets only MouseFilter
    }
    public class NEventLayout : Godot.Control
    {
        protected MegaCrit.Sts2.Core.Models.EventModel _event;
        public NEventLayout(MegaCrit.Sts2.Core.Models.EventModel model) => _event = model;
        public List<NEventOptionButton> Buttons { get; } = new();
        public IEnumerable<NEventOptionButton> OptionButtons => Buttons;
        public Godot.Control Content { get; } = new();
        public Godot.Control? DefaultFocusedControl => Buttons.FirstOrDefault();
        public int Resumes { get; private set; }
        public Action? NativeAfterResume { get; set; }
        public T? GetNodeOrNull<T>(string name) where T : Godot.Control => Content as T;
        public void DisableEventOptions() { foreach (var button in Buttons) button.Disable(); }
        public void AddChild(Godot.Control child) { }
        public virtual void OnSetupComplete()
        {
            Resumes++;
            foreach (var button in Buttons) button.EnableButton();
            NativeAfterResume?.Invoke();
        }
    }
    public sealed class NAncientEventLayout(MegaCrit.Sts2.Core.Models.EventModel model) : NEventLayout(model)
    {
        public IReadOnlyList<MegaCrit.Sts2.Core.Entities.Ancients.AncientDialogueLine> Lines { get; private set; } = Array.Empty<MegaCrit.Sts2.Core.Entities.Ancients.AncientDialogueLine>();
        public void SetDialogue(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Ancients.AncientDialogueLine> lines) { Lines = lines; }
        public void ClearDialogue() { }
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public sealed class NEventRoom : Godot.Control
    {
        private MegaCrit.Sts2.Core.Models.EventModel _event;
        public NEventRoom(MegaCrit.Sts2.Core.Models.EventModel model) => _event = model;
        public Godot.Control? DefaultFocusedControl { get; set; }
        public void OptionButtonClicked(MegaCrit.Sts2.Core.Events.EventOption option, int index) { }
    }
}
namespace ShinGetterMod.Services
{
    internal sealed class ShinGetterBondSession
    {
        internal static bool FailConstruction;
        internal static bool FailEligibility;
        internal ShinGetterBondSession(MegaCrit.Sts2.Core.Models.EventModel model)
        {
            if (FailConstruction) throw new IOException("injected session construction failure");
            Model = model;
        }
        internal MegaCrit.Sts2.Core.Models.EventModel Model { get; }
        internal static bool IsEligible(MegaCrit.Sts2.Core.Models.EventModel model) => FailEligibility
            ? throw new InvalidDataException("injected catalogue failure") : model.Eligible;
    }
}
namespace ShinGetterMod.Nodes.Events
{
    internal sealed class NShinGetterBondDialogue : Godot.Control
    {
        public static NShinGetterBondDialogue Last { get; private set; } = null!;
        private Action? _return;
        private Action? _fallback;
        internal Godot.Control? DefaultFocusedControl => this;
        internal bool IsUpdatingNativeUi { get; set; }
        internal int AdvanceCalls { get; private set; }
        internal int LocalChoiceCalls { get; private set; }
        internal MegaCrit.Sts2.Core.Events.EventOption? LocalOption { get; set; }
        private List<(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton Button, bool Visible)> _hidden = new();
        internal static NShinGetterBondDialogue Create(ShinGetterMod.Services.ShinGetterBondSession session,
            MegaCrit.Sts2.Core.Models.EventModel model, MegaCrit.Sts2.Core.Nodes.Events.NEventLayout layout, Action resume, string? finalAncientSfx = null, Action? fallbackToLegacy = null)
        {
            Last = new() { _return = resume, _fallback = fallbackToLegacy };
            foreach (var button in layout.Buttons) { Last._hidden.Add((button, button.Visible)); button.Hide(); }
            return Last;
        }
        internal static int RewardLayoutRestores { get; private set; }
        internal static void RestoreRewardPage(MegaCrit.Sts2.Core.Nodes.Events.NAncientEventLayout layout, ShinGetterMod.Services.ShinGetterBondSession session)
            => RewardLayoutRestores++; // Dispatch only, never a rendered/native layout assertion.
        internal bool TryChooseOption(MegaCrit.Sts2.Core.Events.EventOption option)
        {
            if (!ReferenceEquals(LocalOption, option)) return false;
            LocalChoiceCalls++;
            return true;
        }
        internal void AdvanceFromNativeHitbox() => AdvanceCalls++;
        internal void Return()
        {
            foreach (var snapshot in _hidden) snapshot.Button.Visible = snapshot.Visible;
            _hidden.Clear();
            _return?.Invoke();
        } // environment stand-in; production UI lifecycle is not executed by this fixture
        internal void Fail()
        {
            foreach (var snapshot in _hidden) snapshot.Button.Visible = snapshot.Visible;
            _hidden.Clear();
            _fallback?.Invoke();
        }
    }
}
