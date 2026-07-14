#nullable enable
using System;
using System.IO;
using System.Linq;
using Godot;

namespace ShinGetterMod.Nodes.Combat;

internal static class NShinGetterSpriteSequence
{
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
    public const string ShinDragonIdleFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_idle";
    public const string ShinDragonAttackFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_attack";
    public const string ShinDragonCastFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_cast";
    public const string ShinDragonBlockFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_block";
    public const string ShinDragonDashFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_dash";
    public const string ShinDragonDeathFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_death";
    public const string IdleAnimationName = "idle";
    public const string AttackAnimationName = "attack";
    public const string HeavyAttackAnimationName = "heavy_attack";
    public const string CastAnimationName = "cast";
    public const string DashAnimationName = "dash";
    public const string BlockAnimationName = "block";
    public const string DeathAnimationName = "death";
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
    public const int ShinDragonBlockMaxFrames = 121;
    public const int ShinDragonDashMaxFrames = 48;
    public const int ShinDragonDeathMaxFrames = 48;
    public const double IdleFramesPerSecond = 24d;
    public const double AttackFramesPerSecond = 36d;
    public const double ActionFramesPerSecond = 30d;
    public const double GetterOneBlockFramesPerSecond = 45d;
    public const double ShinDragonAttackFramesPerSecond = 54d;
    public const double ShinDragonBlockFramesPerSecond = 60d;

    public const string FrameDirectory = IdleFrameDirectory;
    public const string AnimationName = IdleAnimationName;
    public const int MaxFrames = IdleMaxFrames;
    public const double FramesPerSecond = IdleFramesPerSecond;

    public static void EnsureLoaded(AnimatedSprite2D sprite)
    {
        EnsureIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        LoadLinearAnimation(frames, AttackAnimationName, AttackFrameDirectory, AttackMaxFrames, AttackFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, CastAnimationName, CastFrameDirectory, CastMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, BlockAnimationName, GetterOneBlockFrameDirectory, GetterOneBlockMaxFrames, GetterOneBlockFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DashAnimationName, GetterOneDashFrameDirectory, GetterOneDashMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DeathAnimationName, DeathFrameDirectory, DeathMaxFrames, ActionFramesPerSecond, loop: false);
    }

    public static void EnsureIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, IdleFrameDirectory, IdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
    }

    public static void EnsureGetterTwoLoaded(AnimatedSprite2D sprite)
    {
        EnsureGetterTwoIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        LoadLinearAnimation(frames, AttackAnimationName, GetterTwoAttackFrameDirectory, GetterTwoAttackMaxFrames, AttackFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, CastAnimationName, GetterTwoCastFrameDirectory, GetterTwoCastMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, BlockAnimationName, GetterTwoBlockFrameDirectory, GetterTwoBlockMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DashAnimationName, GetterTwoDashFrameDirectory, GetterTwoDashMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DeathAnimationName, GetterTwoDeathFrameDirectory, GetterTwoDeathMaxFrames, ActionFramesPerSecond, loop: false);
    }

    public static void EnsureGetterTwoIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, GetterTwoIdleFrameDirectory, GetterTwoIdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
    }

    public static void EnsureGetterThreeLoaded(AnimatedSprite2D sprite)
    {
        EnsureGetterThreeIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        LoadLinearAnimation(frames, AttackAnimationName, GetterThreeAttackFrameDirectory, GetterThreeAttackMaxFrames, AttackFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DashAnimationName, GetterThreeDashFrameDirectory, GetterThreeDashMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, CastAnimationName, GetterThreeCastFrameDirectory, GetterThreeCastMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, BlockAnimationName, GetterThreeBlockFrameDirectory, GetterThreeBlockMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DeathAnimationName, GetterThreeDeathFrameDirectory, GetterThreeDeathMaxFrames, ActionFramesPerSecond, loop: false);
    }

    public static void EnsureGetterThreeIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, GetterThreeIdleFrameDirectory, GetterThreeIdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
    }

    public static void EnsureShinDragonLoaded(AnimatedSprite2D sprite)
    {
        EnsureShinDragonIdleLoaded(sprite);
        SpriteFrames frames = sprite.SpriteFrames;
        LoadLinearAnimation(frames, AttackAnimationName, ShinDragonAttackFrameDirectory, ShinDragonAttackMaxFrames, ShinDragonAttackFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, CastAnimationName, ShinDragonCastFrameDirectory, ShinDragonCastMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, BlockAnimationName, ShinDragonBlockFrameDirectory, ShinDragonBlockMaxFrames, ShinDragonBlockFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DashAnimationName, ShinDragonDashFrameDirectory, ShinDragonDashMaxFrames, ActionFramesPerSecond, loop: false);
        LoadLinearAnimation(frames, DeathAnimationName, ShinDragonDeathFrameDirectory, ShinDragonDeathMaxFrames, ActionFramesPerSecond, loop: false);
    }

    public static void EnsureShinDragonIdleLoaded(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames ?? new SpriteFrames();
        sprite.SpriteFrames = frames;
        LoadPingPongAnimation(frames, IdleAnimationName, ShinDragonIdleFrameDirectory, ShinDragonIdleMaxFrames, IdleFramesPerSecond, loop: true);

        if (!sprite.IsPlaying() && frames.HasAnimation(IdleAnimationName))
            sprite.Play(IdleAnimationName);
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
        using DirAccess? directory = DirAccess.Open(frameDirectory);
        if (directory == null)
        {
            GD.PushWarning($"Shin Getter sprite sequence directory missing: {frameDirectory}");
            return Array.Empty<Texture2D>();
        }

        string normalizedDirectory = frameDirectory.TrimEnd('/');
        return directory.GetFiles()
            .Where(IsFrameResourceFile)
            .Select(NormalizeFrameResourceFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Take(maxFrames)
            .Select(frameFile => ResourceLoader.Load<Texture2D>($"{normalizedDirectory}/{frameFile}", null, ResourceLoader.CacheMode.Reuse))
            .Where(texture => texture != null)
            .Cast<Texture2D>()
            .ToArray();
    }

    private static bool IsFrameResourceFile(string file) =>
        file.StartsWith("sprite_", StringComparison.OrdinalIgnoreCase)
        && (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".png.remap", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".png.import", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeFrameResourceFile(string file) =>
        file.EndsWith(".remap", StringComparison.OrdinalIgnoreCase)
            ? file[..^".remap".Length]
            : file.EndsWith(".import", StringComparison.OrdinalIgnoreCase)
                ? file[..^".import".Length]
            : file;

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
}
