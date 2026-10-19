// CPU signal/Tween doubles. No Godot assembly, native engine, resource loader or renderer is loaded.
namespace Godot
{
    public class GodotObject
    {
        public bool ObjectValid = true;
        public static bool IsInstanceValid(GodotObject? value) => value?.ObjectValid == true;
    }
    public sealed class SceneTree : GodotObject
    {
        public event Action? ProcessFrame;
        public int Subscribers => ProcessFrame?.GetInvocationList().Length ?? 0;
        public void Pulse() => ProcessFrame?.Invoke();
    }
    public class Node : GodotObject
    {
        public event Action? TreeExiting;
        public int Subscribers => TreeExiting?.GetInvocationList().Length ?? 0;
        public bool Inside = true;
        public readonly SceneTree Tree = new();
        public readonly List<Tween> Tweens = new();
        public bool IsInsideTree() => Inside;
        public bool IsQueuedForDeletion() => false;
        public SceneTree GetTree() => Tree;
        public Tween CreateTween() { var tween = new Tween(); Tweens.Add(tween); return tween; }
        public Task ToSignal(Tween tween, string signal)
        {
            var completion = new TaskCompletionSource();
            void Finish() { tween.Finished -= Finish; completion.SetResult(); }
            tween.Finished += Finish;
            return completion.Task;
        }
        public void Exit() { TreeExiting?.Invoke(); Inside = false; }
    }
    public sealed class Callable
    {
        internal Action<float> Invoke = null!;
        internal Action? Callback;
        public static Callable From<T>(Action<T> action) => new() { Invoke = value => action((T)(object)value) };
        public static Callable From(Action action) => new() { Callback = action };
    }
    public sealed class MethodTweener
    {
        public MethodTweener SetTrans(Tween.TransitionType value) => this;
    }
    public sealed class PropertyTweener
    {
        public PropertyTweener SetEase(Tween.EaseType value) => this;
        public PropertyTweener SetTrans(Tween.TransitionType value) => this;
        public PropertyTweener SetDelay(double value) => this;
    }
    public class Tween : GodotObject
    {
        public enum TransitionType { Linear, Cubic, Sine, Back }
        public enum EaseType { In, Out }
        public static class SignalName { public const string Finished = "finished"; }
        public event Action? Finished;
        private Action<float>? _advance;
        private readonly List<Action<float>> _properties = new();
        private readonly List<Action> _callbacks = new();
        private bool _valid = true;
        public int Subscribers => Finished?.GetInvocationList().Length ?? 0;
        public bool Running;
        public int Kills;
        public double Duration;
        public Tween SetParallel() => this;
        public void Pause() => Running = false;
        public PropertyTweener TweenProperty(CanvasItem node, string key, object value, double duration)
        {
            Duration = Math.Max(Duration, duration);
            if (key == "global_position")
            {
                var target = (Node2D)node; var start = target.GlobalPosition; var end = (Vector2)value;
                _properties.Add(progress => target.GlobalPosition = start.Lerp(end, progress));
            }
            return new();
        }
        public PropertyTweener TweenCallback(Callable action) { _callbacks.Add(action.Callback!); return new(); }
        public bool IsValid() => _valid;
        public void Play() => Running = true;
        public void Kill() { Kills++; Running = false; _valid = false; }
        public MethodTweener TweenMethod(Callable action, float from, float to, double duration)
        {
            if (from != 0 || to != 1) throw new Exception("Expected normalized single clock");
            Duration = duration;
            _advance = action.Invoke;
            return new();
        }
        public void Tick(float progress)
        {
            if (Running && _valid) { foreach (var property in _properties) property(progress); _advance?.Invoke(progress); }
        }
        public void Complete() { Tick(1); foreach (var callback in _callbacks) callback(); Finished?.Invoke(); }
    }
    public readonly record struct Vector2(float X, float Y)
    {
        public static Vector2 Left => new(-1, 0);
        public static Vector2 Right => new(1, 0);
        public static Vector2 One => new(1, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator *(Vector2 value, float scale) => new(value.X * scale, value.Y * scale);
        public Vector2 Lerp(Vector2 target, float progress) => new(X + (target.X - X) * progress, Y + (target.Y - Y) * progress);
    }
    public readonly record struct Color(float A)
    {
        public Color(Color color, float alpha) : this(alpha) { }
    }
    public readonly record struct StringName(string Value)
    {
        public static implicit operator StringName(string name) => new(name);
    }
    public class CanvasItem : Node { public bool Visible; public Color Modulate = new(1); }
    public class Node2D : CanvasItem
    {
        public string Name = "";
        public Vector2 Scale = Vector2.One;
        public Vector2 GlobalPosition;
        public float RotationDegrees;
    }
    public sealed class SpriteFrames
    {
        public double[] Units = new[] { 1d, 2d, 1d };
        public double Fps = 60;
        public int GetFrameCount(StringName name) => Units.Length;
        public double GetFrameDuration(StringName name, int frame) => Units[frame];
        public double GetAnimationSpeed(StringName name) => Fps;
    }
    public sealed class AnimatedSprite2D : Node2D
    {
        public SpriteFrames? SpriteFrames = new();
        public StringName Animation;
        private int _frame;
        public bool ThrowOnFrameUpdate;
        public int Frame
        {
            get => _frame;
            set { if (ThrowOnFrameUpdate) throw new InvalidOperationException("frame-write"); _frame = value; }
        }
        public float FrameProgress;
        public float SpeedScale = 1;
        public bool FusionAvailable = true;
        public int IdleCalls;
        public void Stop() { }
    }
}
namespace MegaCrit.Sts2.Core.Helpers { public static class NonInteractiveMode { public static bool IsActive; } }
namespace MegaCrit.Sts2.Core.Settings { public enum FastModeType { Normal, Fast, Instant } }
namespace MegaCrit.Sts2.Core.Saves
{
    public sealed class Prefs { public MegaCrit.Sts2.Core.Settings.FastModeType FastMode; }
    public sealed class SaveManager
    {
        public static readonly SaveManager Instance = new();
        public readonly Prefs PrefsSave = new();
    }
}
namespace MegaCrit.Sts2.Core.Combat
{
    public sealed class CombatManager
    {
        public static readonly CombatManager Instance = new();
        public bool IsEnding;
    }
}
