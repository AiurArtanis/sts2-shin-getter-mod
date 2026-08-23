#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Nodes.Combat;

internal static class NShinGetterSpriteSequence
{
    private const int MaxCachedActionAnimations = 2;
    private const int FrameSize = 720;
    private const string SpriteSheetFileName = "sprite_sheet.png";

    private static readonly (string Directory, int MaxFrames)[] StartupPreloadSources =
    {
        (AttackFrameDirectory, AttackMaxFrames),
        (ShinDragonIdleFrameDirectory, ShinDragonIdleMaxFrames),
        (GetterOneFusionFrameDirectory, FusionMaxFrames),
        (GetterTwoFusionFrameDirectory, FusionMaxFrames),
        (GetterThreeFusionFrameDirectory, FusionMaxFrames),
    };

    private static readonly ConditionalWeakTable<AnimatedSprite2D, ActionCacheState> ActionCaches = new();

    public const string IdleFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_idle";
    public const string AttackFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_attack";
    public const string CastFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_cast";
    public const string GetterOneBlockFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_block";
    public const string GetterOneDashFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_dash";
    public const string DeathFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_death";
    public const string GetterTwoIdleFrameDirectory = "res://images/characters/shin_getter/forms/getter_two_idle";
    public const string GetterTwoAttackFrameDirectory = "res://images/characters/shin_getter/forms/getter_two_attack";
    public const string GetterTwoCastFrameDirectory = "res://images/characters/shin_getter/forms/getter_two_cast";
    public const string GetterTwoBlockFrameDirectory = "res://images/characters/shin_getter/forms/getter_two_block";
    public const string GetterTwoDashFrameDirectory = "res://images/characters/shin_getter/forms/getter_two_dash";
    public const string GetterTwoDeathFrameDirectory = "res://images/characters/shin_getter/forms/getter_two_death";
    public const string GetterThreeIdleFrameDirectory = "res://images/characters/shin_getter/forms/getter_three_idle";
    public const string GetterThreeAttackFrameDirectory = "res://images/characters/shin_getter/forms/getter_three_attack";
    public const string GetterThreeDashFrameDirectory = "res://images/characters/shin_getter/forms/getter_three_dash";
    public const string GetterThreeCastFrameDirectory = "res://images/characters/shin_getter/forms/getter_three_cast";
    public const string GetterThreeBlockFrameDirectory = "res://images/characters/shin_getter/forms/getter_three_block";
    public const string GetterThreeDeathFrameDirectory = "res://images/characters/shin_getter/forms/getter_three_death";
    public const string GetterOneFusionFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_fusion";
    public const string GetterOneStonerSunshineFrameDirectory = "res://images/characters/shin_getter/forms/getter_one_stoner_sunshine";
    public const string GetterTwoFusionFrameDirectory = "res://images/characters/shin_getter/forms/getter_two_fusion";
    public const string GetterThreeFusionFrameDirectory = "res://images/characters/shin_getter/forms/getter_three_fusion";
    public const string ShinDragonIdleFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_idle";
    public const string ShinDragonAttackFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_attack";
    public const string ShinDragonCastFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_cast";
    public const string ShinDragonBlockFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_block";
    public const string ShinDragonDashFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_dash";
    public const string ShinDragonDeathFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_death";
    public const string ShinDragonStonerSunshineFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_stoner_sunshine";
    public const string IdleAnimationName = "idle";
    public const string AttackAnimationName = "attack";
    public const string HeavyAttackAnimationName = "heavy_attack";
    public const string CastAnimationName = "cast";
    public const string DashAnimationName = "dash";
    public const string BlockAnimationName = "block";
    public const string DeathAnimationName = "death";
    public const string FusionAnimationName = "fusion";
    public const string StonerSunshineAnimationName = "stoner_sunshine";
    public const int IdleMaxFrames = 24;
    public const int AttackMaxFrames = 40;
    public const int CastMaxFrames = 32;
    public const int GetterOneBlockMaxFrames = 24;
    public const int GetterOneDashMaxFrames = 48;
    public const int DeathMaxFrames = 48;
    public const int GetterTwoIdleMaxFrames = 24;
    public const int GetterTwoAttackMaxFrames = 40;
    public const int GetterTwoCastMaxFrames = 32;
    public const int GetterTwoBlockMaxFrames = 24;
    public const int GetterTwoDashMaxFrames = 48;
    public const int GetterTwoDeathMaxFrames = 48;
    public const int GetterThreeIdleMaxFrames = 24;
    public const int GetterThreeAttackMaxFrames = 40;
    public const int GetterThreeDashMaxFrames = 48;
    public const int GetterThreeCastMaxFrames = 32;
    public const int GetterThreeBlockMaxFrames = 24;
    public const int GetterThreeDeathMaxFrames = 48;
    public const int ShinDragonIdleMaxFrames = 36;
    public const int ShinDragonAttackMaxFrames = 60;
    public const int ShinDragonCastMaxFrames = 32;
    public const int ShinDragonBlockMaxFrames = 48;
    public const int ShinDragonDashMaxFrames = 48;
    public const int ShinDragonDeathMaxFrames = 48;
    public const int FusionMaxFrames = 30;
    public const int StonerSunshineMaxFrames = 60;
    public const double IdleFramesPerSecond = 24d;
    public const double AttackFramesPerSecond = 36d;
    public const double ActionFramesPerSecond = 30d;
    public const double GetterOneBlockFramesPerSecond = 45d;
    public const double ShinDragonAttackFramesPerSecond = 54d;
    public const double ShinDragonBlockFramesPerSecond = 60d;
    public const double FusionFramesPerSecond = 60d;
    public const double StonerSunshineFramesPerSecond = 30d;

    public const string FrameDirectory = IdleFrameDirectory;
    public const string AnimationName = IdleAnimationName;
    public const int MaxFrames = IdleMaxFrames;
    public const double FramesPerSecond = IdleFramesPerSecond;

    public static IEnumerable<string> GetStartupPreloadResourcePaths() =>
        StartupPreloadSources.Select(source => GetSpriteSheetResourcePath(source.Directory));

    public static void EnsureLoaded(AnimatedSprite2D sprite, string animationName)
    {
        EnsureIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        EnsureRequestedAnimation(frames, animationName,
            (AttackAnimationName, AttackFrameDirectory, AttackMaxFrames, AttackFramesPerSecond),
            (CastAnimationName, CastFrameDirectory, CastMaxFrames, ActionFramesPerSecond),
            (StonerSunshineAnimationName, GetterOneStonerSunshineFrameDirectory, StonerSunshineMaxFrames, StonerSunshineFramesPerSecond),
            (BlockAnimationName, GetterOneBlockFrameDirectory, GetterOneBlockMaxFrames, GetterOneBlockFramesPerSecond),
            (DashAnimationName, GetterOneDashFrameDirectory, GetterOneDashMaxFrames, ActionFramesPerSecond),
            (DeathAnimationName, DeathFrameDirectory, DeathMaxFrames, ActionFramesPerSecond));
        TrackAndTrimActionCache(sprite, animationName);
    }

    public static void EnsureIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, IdleFrameDirectory, IdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
    }

    public static void EnsureGetterTwoLoaded(AnimatedSprite2D sprite, string animationName)
    {
        EnsureGetterTwoIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        EnsureRequestedAnimation(frames, animationName,
            (AttackAnimationName, GetterTwoAttackFrameDirectory, GetterTwoAttackMaxFrames, AttackFramesPerSecond),
            (CastAnimationName, GetterTwoCastFrameDirectory, GetterTwoCastMaxFrames, ActionFramesPerSecond),
            (BlockAnimationName, GetterTwoBlockFrameDirectory, GetterTwoBlockMaxFrames, ActionFramesPerSecond),
            (DashAnimationName, GetterTwoDashFrameDirectory, GetterTwoDashMaxFrames, ActionFramesPerSecond),
            (DeathAnimationName, GetterTwoDeathFrameDirectory, GetterTwoDeathMaxFrames, ActionFramesPerSecond));
        TrackAndTrimActionCache(sprite, animationName);
    }

    public static void EnsureGetterTwoIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, GetterTwoIdleFrameDirectory, GetterTwoIdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
    }

    public static void EnsureGetterThreeLoaded(AnimatedSprite2D sprite, string animationName)
    {
        EnsureGetterThreeIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        EnsureRequestedAnimation(frames, animationName,
            (AttackAnimationName, GetterThreeAttackFrameDirectory, GetterThreeAttackMaxFrames, AttackFramesPerSecond),
            (CastAnimationName, GetterThreeCastFrameDirectory, GetterThreeCastMaxFrames, ActionFramesPerSecond),
            (BlockAnimationName, GetterThreeBlockFrameDirectory, GetterThreeBlockMaxFrames, ActionFramesPerSecond),
            (DashAnimationName, GetterThreeDashFrameDirectory, GetterThreeDashMaxFrames, ActionFramesPerSecond),
            (DeathAnimationName, GetterThreeDeathFrameDirectory, GetterThreeDeathMaxFrames, ActionFramesPerSecond));
        TrackAndTrimActionCache(sprite, animationName);
    }

    public static void EnsureGetterThreeIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, GetterThreeIdleFrameDirectory, GetterThreeIdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
    }

    public static void EnsureShinDragonLoaded(AnimatedSprite2D sprite, string animationName)
    {
        EnsureShinDragonIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        EnsureRequestedAnimation(frames, animationName,
            (AttackAnimationName, ShinDragonAttackFrameDirectory, ShinDragonAttackMaxFrames, ShinDragonAttackFramesPerSecond),
            (CastAnimationName, ShinDragonCastFrameDirectory, ShinDragonCastMaxFrames, ActionFramesPerSecond),
            (StonerSunshineAnimationName, ShinDragonStonerSunshineFrameDirectory, StonerSunshineMaxFrames, StonerSunshineFramesPerSecond),
            (BlockAnimationName, ShinDragonBlockFrameDirectory, ShinDragonBlockMaxFrames, ShinDragonBlockFramesPerSecond),
            (DashAnimationName, ShinDragonDashFrameDirectory, ShinDragonDashMaxFrames, ActionFramesPerSecond),
            (DeathAnimationName, ShinDragonDeathFrameDirectory, ShinDragonDeathMaxFrames, ActionFramesPerSecond));
        TrackAndTrimActionCache(sprite, animationName);
    }

    public static void EnsureShinDragonIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, ShinDragonIdleFrameDirectory, ShinDragonIdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
    }

    /// <summary>
    /// Loads the non-cached atomic fighter-to-form sequence for one of the three Getter forms.
    /// This is intentionally separate from combat action caching: transform playback can reverse it.
    /// </summary>
    public static bool EnsureFusionLoaded(AnimatedSprite2D sprite, ShinGetterForm form)
    {
        string? frameDirectory = form switch
        {
            ShinGetterForm.Getter1 => GetterOneFusionFrameDirectory,
            ShinGetterForm.Getter2 => GetterTwoFusionFrameDirectory,
            ShinGetterForm.Getter3 => GetterThreeFusionFrameDirectory,
            _ => null,
        };
        if (frameDirectory == null)
            return false;

        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadLinearAnimation(
            frames,
            FusionAnimationName,
            frameDirectory,
            FusionMaxFrames,
            FusionFramesPerSecond,
            loop: false);
        return frames.HasAnimation(FusionAnimationName)
               && frames.GetFrameCount(FusionAnimationName) >= FusionMaxFrames;
    }

    public static void ReleaseActionAnimations(AnimatedSprite2D sprite)
    {
        if (sprite.SpriteFrames is not { } frames)
            return;

        foreach (string animationName in ActionAnimationNames)
        {
            if (frames.HasAnimation(animationName))
                frames.RemoveAnimation(animationName);
        }

        ActionCaches.Remove(sprite);
    }

    private static readonly string[] ActionAnimationNames =
    {
        AttackAnimationName,
        HeavyAttackAnimationName,
        CastAnimationName,
        BlockAnimationName,
        DashAnimationName,
        DeathAnimationName,
        StonerSunshineAnimationName,
    };

    private static void EnsureRequestedAnimation(
        SpriteFrames frames,
        string requestedAnimation,
        params (string Name, string Directory, int MaxFrames, double FramesPerSecond)[] sources)
    {
        var source = sources.FirstOrDefault(candidate => candidate.Name == requestedAnimation);
        if (source.Name == null)
            return;

        LoadLinearAnimation(
            frames,
            source.Name,
            source.Directory,
            source.MaxFrames,
            source.FramesPerSecond,
            loop: false);
    }

    private static void TrackAndTrimActionCache(AnimatedSprite2D sprite, string animationName)
    {
        if (animationName == IdleAnimationName || sprite.SpriteFrames?.HasAnimation(animationName) != true)
            return;

        ActionCacheState cache = ActionCaches.GetOrCreateValue(sprite);
        cache.Touch(animationName);
        while (cache.Count > MaxCachedActionAnimations)
        {
            string oldest = cache.RemoveOldest();
            if (sprite.SpriteFrames.HasAnimation(oldest))
                sprite.SpriteFrames.RemoveAnimation(oldest);
        }
    }

    private static void LoadPingPongAnimation(
        SpriteFrames frames,
        string animationName,
        string frameDirectory,
        int maxFrames,
        double framesPerSecond,
        bool loop)
    {
        StringName animationKey = new(animationName);
        int expectedFrameCount = maxFrames * 2;
        if (HasSufficientAnimation(frames, animationKey, expectedFrameCount))
            return;

        Texture2D[] textures = LoadTextures(frameDirectory, maxFrames);
        if (textures.Length == 0)
        {
            GD.PushWarning($"Shin Getter sprite sequence loaded zero usable frames: {frameDirectory}");
            return;
        }

        ReplaceAnimationIfPresent(frames, animationKey);
        frames.AddAnimation(animationKey);
        frames.SetAnimationLoop(animationKey, loop);
        frames.SetAnimationSpeed(animationKey, framesPerSecond);
        AddPingPongFrames(frames, animationKey, textures);
    }

    private static void LoadLinearAnimation(
        SpriteFrames frames,
        string animationName,
        string frameDirectory,
        int maxFrames,
        double framesPerSecond,
        bool loop)
    {
        StringName animationKey = new(animationName);
        int expectedFrameCount = maxFrames;
        if (HasSufficientAnimation(frames, animationKey, expectedFrameCount))
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        ulong memoryBefore = OS.GetStaticMemoryUsage();
        ulong videoMemoryBefore = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed);
        Texture2D[] textures = LoadTextures(frameDirectory, maxFrames);
        if (textures.Length == 0)
        {
            GD.PushWarning($"Shin Getter sprite sequence loaded zero usable frames: {frameDirectory}");
            return;
        }

        ReplaceAnimationIfPresent(frames, animationKey);
        frames.AddAnimation(animationKey);
        frames.SetAnimationLoop(animationKey, loop);
        frames.SetAnimationSpeed(animationKey, framesPerSecond);
        AddLinearFrames(frames, animationKey, textures);
        stopwatch.Stop();
        ulong memoryAfter = OS.GetStaticMemoryUsage();
        ulong videoMemoryAfter = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed);
        GD.Print(
            $"Shin Getter staged animation load: {frameDirectory}, frames={textures.Length}, " +
            $"elapsed={stopwatch.ElapsedMilliseconds}ms, " +
            $"memory_delta={FormatSignedDelta(memoryAfter, memoryBefore)}, " +
            $"vram_delta={FormatSignedDelta(videoMemoryAfter, videoMemoryBefore)}");
    }

    private static bool HasSufficientAnimation(SpriteFrames frames, StringName animationKey, int expectedFrameCount) =>
        frames.HasAnimation(animationKey) && frames.GetFrameCount(animationKey) >= expectedFrameCount;

    private static void ReplaceAnimationIfPresent(SpriteFrames frames, StringName animationKey)
    {
        if (frames.HasAnimation(animationKey))
            frames.RemoveAnimation(animationKey);
    }

    private static Texture2D[] LoadTextures(string frameDirectory, int maxFrames)
    {
        string sheetPath = GetSpriteSheetResourcePath(frameDirectory);
        Texture2D? sheet = ResourceLoader.Load<Texture2D>(sheetPath, null, ResourceLoader.CacheMode.Reuse);
        if (sheet == null)
        {
            GD.PushWarning($"Shin Getter sprite sheet missing: {sheetPath}");
            return Array.Empty<Texture2D>();
        }

        int sheetWidth = sheet.GetWidth();
        int sheetHeight = sheet.GetHeight();
        if (sheetWidth % FrameSize != 0 || sheetHeight % FrameSize != 0)
        {
            GD.PushWarning($"Shin Getter sprite sheet has an invalid cell grid: {sheetPath}, size={sheetWidth}x{sheetHeight}");
            return Array.Empty<Texture2D>();
        }

        int columns = sheetWidth / FrameSize;
        int rows = sheetHeight / FrameSize;
        if (columns * rows < maxFrames)
        {
            GD.PushWarning($"Shin Getter sprite sheet has insufficient cells: {sheetPath}, cells={columns * rows}, expected={maxFrames}");
            return Array.Empty<Texture2D>();
        }

        return Enumerable.Range(0, maxFrames)
            .Select(index => (Texture2D)new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2(
                    index % columns * FrameSize,
                    index / columns * FrameSize,
                    FrameSize,
                    FrameSize),
                FilterClip = true,
            })
            .ToArray();
    }

    private static string GetSpriteSheetResourcePath(string frameDirectory) =>
        $"{frameDirectory.TrimEnd('/')}/{SpriteSheetFileName}";

    private static void AddPingPongFrames(SpriteFrames frames, StringName animationKey, Texture2D[] textures)
    {
        foreach (Texture2D texture in textures)
            frames.AddFrame(animationKey, texture);

        for (int index = textures.Length - 1; index >= 0; index--)
            frames.AddFrame(animationKey, textures[index]);
    }

    private static void AddLinearFrames(SpriteFrames frames, StringName animationKey, Texture2D[] textures)
    {
        foreach (Texture2D texture in textures)
            frames.AddFrame(animationKey, texture);
    }

    private static string FormatSignedDelta(ulong after, ulong before)
    {
        long delta = after >= before
            ? checked((long)(after - before))
            : -checked((long)(before - after));
        return $"{delta / 1024d / 1024d:+0.0;-0.0;0.0} MiB";
    }

    private sealed class ActionCacheState
    {
        private readonly LinkedList<string> _order = new();

        public int Count => _order.Count;

        public void Touch(string animationName)
        {
            _order.Remove(animationName);
            _order.AddLast(animationName);
        }

        public string RemoveOldest()
        {
            string oldest = _order.First!.Value;
            _order.RemoveFirst();
            return oldest;
        }
    }
}
