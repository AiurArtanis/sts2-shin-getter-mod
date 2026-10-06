// Managed environment stand-ins ONLY. The complete production bridge is linked
// unchanged, with actual Harmony FieldRef. This is not native Godot/input proof.
namespace Godot
{
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
namespace MegaCrit.Sts2.Core.Entities.Ancients { public enum ArchitectAttackers { Player } }
namespace MegaCrit.Sts2.Core.Events
{
    public sealed class EventOption { public bool IsLocked { get; set; } }
    public sealed class AncientDialogue(string text) { public string Text { get; } = text; public MegaCrit.Sts2.Core.Entities.Ancients.ArchitectAttackers EndAttackers { get; set; } }
}
namespace MegaCrit.Sts2.Core.Localization { public sealed class LocString(string table, string key) { public string Table { get; } = table; public string Key { get; } = key; } }
namespace MegaCrit.Sts2.Core.Models
{
    public class EventModel
    {
        public Godot.Control? Node { get; set; }
        public bool Eligible { get; set; } = true;
        protected void SetEventState(MegaCrit.Sts2.Core.Localization.LocString description, IEnumerable<MegaCrit.Sts2.Core.Events.EventOption> options) { }
    }
}
namespace MegaCrit.Sts2.Core.Models.Events
{
    public sealed class TheArchitect : MegaCrit.Sts2.Core.Models.EventModel
    {
        private MegaCrit.Sts2.Core.Events.AncientDialogue? _dialogue;
        public MegaCrit.Sts2.Core.Events.AncientDialogue? StoredDialogue => _dialogue;
        public void LoadDialogue() => _dialogue = null;
        public Task PlayCurrentLine() => Task.CompletedTask;
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
        public void SetDialogue() { }
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
    internal sealed class ShinGetterBondSession(MegaCrit.Sts2.Core.Models.EventModel model)
    {
        internal MegaCrit.Sts2.Core.Models.EventModel Model { get; } = model;
        internal static bool IsEligible(MegaCrit.Sts2.Core.Models.EventModel model) => model.Eligible;
    }
}
namespace ShinGetterMod.Nodes.Events
{
    internal sealed class NShinGetterBondDialogue : Godot.Control
    {
        public static NShinGetterBondDialogue Last { get; private set; } = null!;
        private Action? _return;
        internal Godot.Control? DefaultFocusedControl => this;
        internal static NShinGetterBondDialogue Create(ShinGetterMod.Services.ShinGetterBondSession session, Action resume)
            => Last = new() { _return = resume };
        internal void Return() => _return?.Invoke(); // common completion/skip/recovered-closed callback
    }
}
