using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string name, Type[] args) { }
    }
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute { }
}

namespace Godot
{
    public sealed class Vector2I { public Vector2I(int x, int y) { } }
    public sealed class Vector2 { public Vector2(int x, int y) { } }
    public class Resource { }
    public class Texture2D : Resource { }
    public class GodotObject
    {
        internal bool Freed;
        public static bool IsInstanceValid(GodotObject item) { ThreadGuard.Check(); return !item.Freed; }
    }
    public class Node : GodotObject
    {
        private readonly List<Node> children = new();
        private Node? parent;
        private bool inTree;
        private bool queued;
        public string Name { get; set; } = "";
        public event Action? TreeExiting;
        public int ExitListenerCount => TreeExiting?.GetInvocationList().Length ?? 0;
        public SceneTree GetTree() { ThreadGuard.Check(); return SceneTree.Instance; }
        public List<Node> GetChildren() => children.ToList();
        public bool IsInsideTree() => inTree;
        public bool IsQueuedForDeletion() => queued;
        public void SetRoot() => inTree = true;
        public void AddChild(Node child) { ThreadGuard.Check(); child.parent = this; child.inTree = inTree; children.Add(child); }
        public virtual Node Duplicate(int flags) => new Node();
        public void QueueFree()
        {
            ThreadGuard.Check();
            queued = true;
            if (inTree) TreeExiting?.Invoke();
            inTree = false;
            foreach (Node child in children.ToArray()) child.QueueFree();
            parent?.children.Remove(this);
            Freed = true;
        }
        public void Free() => QueueFree();
    }
    public sealed class SceneTree : GodotObject
    {
        public static SceneTree Instance { get; } = new();
        public event Action? ProcessFrame;
        public int ListenerCount => ProcessFrame?.GetInvocationList().Length ?? 0;
        public void EmitProcessFrame() { ThreadGuard.Check(); ProcessFrame?.Invoke(); }
    }
    public class Viewport : Node
    {
        public bool UseHdr2D { get; set; }
        public int Msaa2D { get; set; }
    }
    public class SubViewport : Viewport
    {
        public enum UpdateMode { Always }
        public Vector2I? Size { get; set; }
        public bool Disable3D { get; set; }
        public bool TransparentBg { get; set; }
        public UpdateMode RenderTargetUpdateMode { get; set; }
    }
    public class GpuParticles2D : Node
    {
        public bool Visible { get; set; } = true;
        public Resource ProcessMaterial { get; set; } = new();
        public Resource? Material { get; set; }
        public Texture2D? Texture { get; set; }
        public Vector2? Position { get; set; }
        public override Node Duplicate(int flags) => new GpuParticles2D();
        public void Restart() { }
    }
    public class PackedScene : Resource
    {
        public Node Instantiate() { var n = new Node(); n.AddChild(new GpuParticles2D()); return n; }
    }
    public static class DisplayServer { public static string Name = "Windows"; public static string GetName() { ThreadGuard.Check(); return Name; } }
    public static class RenderingServer
    {
        public static bool RenderLoopEnabled { get; set; } = true;
        public static event Action? FramePostDraw;
        public static int ListenerCount => FramePostDraw?.GetInvocationList().Length ?? 0;
        public static void EmitFramePostDraw() { ThreadGuard.Check(); FramePostDraw?.Invoke(); }
    }
    public static class GD
    {
        public static void Print(string text) => Console.WriteLine(text);
        public static void PushWarning(string text) => Console.WriteLine("WARNING: " + text);
    }
}

namespace MegaCrit.Sts2.Core.Assets
{
    public sealed class FakeCache
    {
        public bool Fail;
        public int Loads;
        public PackedScene GetScene(string path) { ThreadGuard.Check(); Loads++; if (Fail) throw new InvalidOperationException("optional-cache-fault"); return new(); }
    }
    public static class PreloadManager
    {
        public static FakeCache Cache { get; } = new();
        public static Task LoadRoomCombatAssets(MegaCrit.Sts2.Core.Models.EncounterModel encounter, IRunState runState) => Task.CompletedTask;
    }
}
namespace MegaCrit.Sts2.Core.Models { public class EncounterModel { } }
namespace MegaCrit.Sts2.Core.Runs
{
    public sealed class FakePlayer { public object Character { get; init; } = new(); }
    public interface IRunState { List<FakePlayer> Players { get; } }
    public sealed class FakeRun : IRunState
    {
        public List<FakePlayer> Players { get; }
        public FakeRun(bool getter) => Players = new() { new() { Character = getter ? new ShinGetterMod.Models.Characters.ShinGetter() : new object() } };
    }
}
namespace MegaCrit.Sts2.Core.Nodes
{
    public sealed class NGame : Node
    {
        public static NGame? Instance { get; set; }
        public NGame() => SetRoot();
        public Viewport GetViewport() => new();
    }
}
namespace MegaCrit.Sts2.Core.TestSupport { public static class TestMode { public static bool IsOn { get; set; } } }
namespace ShinGetterMod.Models.Characters { public sealed class ShinGetter { } }
