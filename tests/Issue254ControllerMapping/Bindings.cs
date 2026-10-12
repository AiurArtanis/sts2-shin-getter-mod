// Managed environment stand-ins. Policy/Patch source and Harmony FieldRef are production-linked.
namespace Godot
{
    public sealed record StringName(string Value) { public static implicit operator StringName(string s) => new(s); }
    public static class InputMap
    {
        public static HashSet<string> Actions { get; } = new();
        public static int Calls { get; private set; }
        public static bool HasAction(StringName action) { Calls++; return Actions.Contains(action.Value); }
    }
    public static class GD { public static void Print(string message) { } }
}
namespace MegaCrit.Sts2.Core.Nodes.CommonUi
{
    public sealed class NControllerManager { public Dictionary<Godot.StringName, Godot.StringName> GetDefaultControllerInputMap { get; set; } = new(); }
    public sealed class NInputManager
    {
        private Dictionary<Godot.StringName, Godot.StringName> _controllerInputMap = new();
        public NControllerManager ControllerManager { get; } = new();
        public Dictionary<Godot.StringName, Godot.StringName> Runtime => _controllerInputMap;
        public void Install(Dictionary<Godot.StringName, Godot.StringName> map) => _controllerInputMap = map;
        public void _UnhandledInput() { }
        public void GetHotkeyIcon(string hotkey) { }
        public void ModifyControllerButton(Godot.StringName input, Godot.StringName controllerInput) => _controllerInputMap[input] = controllerInput;
    }
}
