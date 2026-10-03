#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Nodes.Vfx;

internal partial class NShinGetterSparkSubtitleFollower : Node
{
    private NSpeechBubbleVfx _subtitle = null!;
    private AnimatedSprite2D _sprite = null!;
    private Creature _owner = null!;
    private const string FollowerName = "ShinGetterSparkSubtitleFollower";
    private const float BodyGap = 24f;
    private const float TextPadding = 12f;
    private MegaRichTextLabel? _nativeText;
    private Node2D? _compactRoot;
    private Label? _compactText;
    private ColorRect? _compactBackground;
    private bool _layoutWarning;

    internal static void Attach(NSpeechBubbleVfx subtitle, Creature owner, bool enlarge)
    {
        if (subtitle.HasNode(FollowerName)) return;
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (creatureNode == null) return;
        foreach (string form in new[] { "ShinDragon", "GetterOne", "GetterTwo", "GetterThree" })
        {
            var sprite = creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/" + form);
            if (sprite == null || !NShinGetterShiningSparkSequence.IsActuallyVisible(sprite)) continue;
            if (!enlarge && sprite.Animation != NShinGetterSpriteSequence.ShiningSparkAnimationName) return;
            Vector2 baseScale = subtitle.Scale;
            subtitle.Scale = baseScale * (enlarge ? 1.3f * 1.5f : 1f);
            // Do not show a frame at the old, stationary TalkPosition before layout is ready.
            subtitle.Hide();
            subtitle.AddChild(new NShinGetterSparkSubtitleFollower
            {
                Name = FollowerName,
                ProcessPriority = 100,
                _subtitle = subtitle,
                _sprite = sprite,
                _owner = owner,
            });
            return;
        }
    }

    public override void _Ready() => RenderingServer.FramePreDraw += OnFramePreDraw;

    public override void _ExitTree() => RenderingServer.FramePreDraw -= OnFramePreDraw;

    private void OnFramePreDraw()
    {
        // Native bubble tweens run after _Process; measure their final transforms before drawing.
        if (!IsInsideTree() || IsQueuedForDeletion() || !GodotObject.IsInstanceValid(_subtitle)
            || _subtitle.IsQueuedForDeletion()) return;
        _Process(0d);
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_sprite) || !_sprite.IsInsideTree()
            || !NShinGetterShiningSparkSequence.IsActuallyVisible(_sprite)
            || _owner.IsDead || CombatManager.Instance.IsOverOrEnding)
        {
            _subtitle.Hide();
            QueueFree();
            return;
        }
        if (CombatManager.Instance.IsPaused) return;
        if (_subtitle.GetParent() is not CanvasItem parent) return;
        Transform2D parentCanvas = parent.GetGlobalTransformWithCanvas();
        if (!parentCanvas.IsFinite() || Mathf.Abs(parentCanvas.Determinant()) < 0.000001f)
        {
            HideUnplaceableSubtitle();
            return;
        }
        FreezeFontSize();
        Rect2 body = NShinGetterShiningSparkSequence.TransformRect(
            NShinGetterShiningSparkSequence.GetFrameLocalRect(_sprite), _sprite.GetGlobalTransformWithCanvas());
        Rect2 bubble = GetBubbleViewportRect();
        Rect2 viewport = _subtitle.GetViewport().GetVisibleRect().Grow(-16f);
        if (_compactRoot != null || _nativeText == null || !ShinGetterSubtitleLayout.IsUsable(bubble)
            || !ShinGetterSubtitleLayout.TryPlace(bubble.Size, body, viewport, BodyGap, out Rect2 placed))
        {
            if (TryCompactLayout(body, viewport)) _subtitle.Show();
            else HideUnplaceableSubtitle();
            return;
        }
        Vector2 shift = placed.Position - bubble.Position;
        Vector2 viewportPosition = _subtitle.GetGlobalTransformWithCanvas().Origin + shift;
        _subtitle.Position = parent.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
        _subtitle.Show();
    }

    private void HideUnplaceableSubtitle()
    {
        _subtitle.Hide();
        if (_layoutWarning) return;
        GD.PushWarning("Shining/Spark subtitle: invalid canvas or no viewport space for the complete text at the requested font size.");
        _layoutWarning = true;
    }

    private void FreezeFontSize()
    {
        if (_nativeText != null) return;
        _nativeText = _subtitle.GetNodeOrNull<MegaRichTextLabel>("%Text");
        if (_nativeText == null) return;
        _nativeText.AutoSizeEnabled = false;
        foreach (string name in new[] { "normal", "bold", "italics", "bold_italics", "mono" })
            _nativeText.AddThemeFontSizeOverride(name + "_font_size", _nativeText.MaxFontSize);
    }

    private bool TryCompactLayout(Rect2 body, Rect2 viewport)
    {
        if (_nativeText == null || !ShinGetterSubtitleLayout.IsUsable(body)
            || !ShinGetterSubtitleLayout.IsUsable(viewport)) return false;
        Transform2D canvas = _subtitle.GetGlobalTransformWithCanvas();
        Vector2 scale = new(canvas.X.Length(), canvas.Y.Length());
        if (!canvas.IsFinite() || !scale.IsFinite() || scale.X < 0.001f || scale.Y < 0.001f
            || Mathf.Abs(canvas.Determinant()) < 0.000001f) return false;
        Font font = _nativeText.GetThemeFont("normal_font");
        int fontSize = _nativeText.MaxFontSize;
        string plainText = _nativeText.GetParsedText();
        if (string.IsNullOrWhiteSpace(plainText)) return false;
        if (_compactRoot == null)
        {
            // A compact, axis-aligned caption avoids the animated background's oversized margins.
            // It inherits the original bubble's fade/lifetime, but never reduces its font or root scale.
            _compactRoot = new Node2D { Name = "CompactSubtitle" };
            _compactBackground = new ColorRect
            {
                Color = new Color(0.03f, 0.03f, 0.03f, 0.9f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _compactText = new Label
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
            };
            _compactText.AddThemeFontOverride("font", font);
            _compactText.AddThemeFontSizeOverride("font_size", fontSize);
            _compactText.AddThemeColorOverride("font_color", _nativeText.GetThemeColor("default_color"));
            _compactText.AddThemeColorOverride("font_outline_color", Colors.Black);
            _compactText.AddThemeConstantOverride("outline_size", 2);
            _subtitle.AddChild(_compactRoot);
            _compactRoot.AddChild(_compactBackground);
            _compactRoot.AddChild(_compactText);
            _subtitle.GetNodeOrNull<CanvasItem>("%Container")?.Hide();
        }
        foreach (Rect2 band in ShinGetterSubtitleLayout.FreeBands(body, viewport, BodyGap))
        {
            if (!ShinGetterSubtitleLayout.IsUsable(band)) continue;
            float width = band.Size.X / scale.X - 2f * TextPadding;
            if (!TryWrapText(plainText, font, fontSize, width, out string wrapped)) continue;
            _compactText!.Text = wrapped;
            Vector2 textSize = _compactText.GetMinimumSize();
            Vector2 localSize = textSize + Vector2.One * (2f * TextPadding);
            if (!ShinGetterSubtitleLayout.TryPlace(localSize * scale, body, viewport, BodyGap, out Rect2 placed)) continue;
            _compactBackground!.Size = localSize;
            _compactText.Position = Vector2.One * TextPadding;
            _compactText.Size = textSize;
            var desired = new Transform2D(new Vector2(scale.X, 0f), new Vector2(0f, scale.Y), placed.Position);
            _compactRoot.Transform = canvas.AffineInverse() * desired;
            return true;
        }
        return false;
    }

    private static bool TryWrapText(string text, Font font, int fontSize, float width, out string wrapped)
    {
        wrapped = string.Empty;
        if (!float.IsFinite(width) || width <= 0f) return false;
        var lines = new List<string>();
        var line = new StringBuilder();
        var elements = StringInfo.GetTextElementEnumerator(text.Replace("\r", string.Empty));
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (element == "\n")
            {
                lines.Add(line.ToString());
                line.Clear();
                continue;
            }
            if (font.GetStringSize(element, HorizontalAlignment.Left, -1f, fontSize).X > width) return false;
            if (line.Length > 0 && font.GetStringSize(line.ToString() + element, HorizontalAlignment.Left, -1f, fontSize).X > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            line.Append(element);
        }
        lines.Add(line.ToString());
        wrapped = string.Join("\n", lines);
        return true;
    }

    private Rect2 GetBubbleViewportRect()
    {
        Rect2? result = null;
        foreach (string name in new[] { "%Bubble", "%Shadow", "%Text" })
        {
            var item = _subtitle.GetNodeOrNull<CanvasItem>(name);
            Rect2 local;
            if (item is Sprite2D sprite) local = sprite.GetRect();
            else if (item is Control control) local = new Rect2(Vector2.Zero, control.Size);
            else continue;
            if (!ShinGetterSubtitleLayout.IsUsable(local)) continue;
            Rect2 rect = NShinGetterShiningSparkSequence.TransformRect(local, item.GetGlobalTransformWithCanvas());
            if (!ShinGetterSubtitleLayout.IsUsable(rect)) continue;
            result = result?.Merge(rect) ?? rect;
        }
        return result ?? default;
    }
}
