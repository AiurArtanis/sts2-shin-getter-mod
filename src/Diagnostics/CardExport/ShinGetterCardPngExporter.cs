#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.TestSupport;
using ShinGetterMod.Models.CardPools;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Patches;

namespace ShinGetterMod.Diagnostics.CardExport;

public static class ShinGetterCardPngExporter
{
    private const string CardScenePath = "res://scenes/cards/card.tscn";
    private const float CardExportHalfExtentX = 190f;
    private const float CardExportHalfExtentY = 240f;
    private const float ExportViewportFramePad = 6f;
    private const int FramesAfterHostAdded = 2;
    private const int FramesAfterVisualRefresh = 2;
    private const int FramesAfterRenderOnce = 5;
    private const int FramesAfterSaveBeforeTeardown = 1;
    private const int FramesAfterTeardown = 2;
    private const int FramesBetweenCards = 1;

    public static void BeginExport(ShinGetterCardPngExportRequest request, Action<string>? log = null)
    {
        if (!TryValidateExportEnvironment(out var error))
        {
            log?.Invoke(error);
            return;
        }

        var req = request;
        var logger = log;
        Callable.From(() => RunExportOnMainThreadEntry(req, logger)).CallDeferred();
    }

    public static bool TryValidateExportEnvironment(out string error)
    {
        if (NGame.Instance == null)
        {
            error = "Game is not ready. Open the main menu or enter a run, then try again.";
            return false;
        }

        if (TestMode.IsOn)
        {
            error = "Card PNG export is unavailable in TestMode.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryNormalizeCharacterFilter(string raw, out string normalized, out string error)
    {
        normalized = raw.Trim().ToUpperInvariant();
        error = string.Empty;

        switch (normalized)
        {
            case "IRONCLAD":
            case "SILENT":
            case "REGENT":
            case "NECROBINDER":
            case "DEFECT":
            case "SHIN_GETTER":
            case "-":
                return true;
            default:
                error = "Invalid character. Use IRONCLAD, SILENT, REGENT, NECROBINDER, DEFECT, SHIN_GETTER, or \"-\".";
                normalized = string.Empty;
                return false;
        }
    }

    public static bool TryNormalizeOutputDirectory(string raw, out string outputPath, out string error)
    {
        outputPath = string.Empty;
        error = string.Empty;

        try
        {
            var trimmed = raw.Trim();
            if (trimmed == "-")
            {
                outputPath = Path.Combine(GetGodotProcessDirectory(), "cards_export");
                return true;
            }

            if (trimmed.StartsWith("user://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = ProjectSettings.GlobalizePath(trimmed);
                return !string.IsNullOrWhiteSpace(outputPath);
            }

            var normalizedSeparators = NormalizePathSeparators(trimmed);
            outputPath = Path.GetFullPath(normalizedSeparators);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Invalid outputDir: {ex.Message}";
            outputPath = string.Empty;
            return false;
        }
    }

    public static List<CardModel> SelectCards(ShinGetterCardPngExportRequest request)
    {
        return GetScopedCards(request.CharacterFilter)
            .Where(c => c is not DeprecatedCard)
            .Where(c => request.IncludeCardsHiddenFromLibrary || c.ShouldShowInCardLibrary)
            .Where(c => MatchesIdFilter(c, request.IdFilterPattern))
            .Distinct()
            .ToList();
    }

    private static async void RunExportOnMainThreadEntry(
        ShinGetterCardPngExportRequest request,
        Action<string>? log)
    {
        try
        {
            await RunExportAsync(request, log);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Shin Getter card PNG export stopped: {ex.Message}");
            GD.PushError($"Shin Getter card PNG export: {ex}");
        }
    }

    private static async Task RunExportAsync(ShinGetterCardPngExportRequest request, Action<string>? log)
    {
        if (!TryValidateExportEnvironment(out var error))
        {
            log?.Invoke(error);
            return;
        }

        var tree = NGame.Instance!.GetTree();
        if (tree == null)
        {
            log?.Invoke("Scene tree is not available.");
            return;
        }

        var outputPath = request.OutputDirectory;
        try
        {
            Directory.CreateDirectory(outputPath);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Could not create output folder: {ex.Message}");
            return;
        }

        var scale = Mathf.Max(0.25f, request.Scale);
        var cards = SelectCards(request);
        log?.Invoke(
            $"Exporting {cards.Count} base card(s) from {request.CharacterFilter} to {outputPath}.");

        var exportedBase = 0;
        var savedFiles = 0;
        var failures = 0;

        foreach (var canonical in cards)
        {
            if (request.MaxBaseCards > 0 && exportedBase >= request.MaxBaseCards)
                break;

            log?.Invoke($"Rendering {exportedBase + 1}/{cards.Count}: {canonical.Id.Entry}");

            var baseFileName = SanitizeFilePart(canonical.Id.Entry) + "_base.png";
            var basePath = Path.Combine(outputPath, baseFileName);
            if (await TryCaptureAsync(tree, canonical, basePath, scale, log, baseFileName))
            {
                savedFiles++;
                log?.Invoke($"Saved {baseFileName}");
            }
            else
            {
                failures++;
                log?.Invoke($"Could not save {baseFileName}.");
            }

            exportedBase++;

            if (request.IncludeUpgradedVariants && canonical.IsUpgradable)
            {
                var upgraded = canonical.ToMutable();
                upgraded.UpgradeInternal();
                var upgradedFileName = SanitizeFilePart(canonical.Id.Entry) + "_upgraded.png";
                var upgradedPath = Path.Combine(outputPath, upgradedFileName);

                if (await TryCaptureAsync(tree, upgraded, upgradedPath, scale, log, upgradedFileName))
                {
                    savedFiles++;
                    log?.Invoke($"Saved {upgradedFileName}");
                }
                else
                {
                    failures++;
                    log?.Invoke($"Could not save {upgradedFileName}.");
                }
            }

            await WaitFrames(tree, FramesBetweenCards);
        }

        log?.Invoke(
            $"Finished Shin Getter card PNG export. {savedFiles} file(s) saved, {failures} failed. Base cards: {exportedBase}. Output: {outputPath}");
    }

    private static IEnumerable<CardModel> GetScopedCards(string characterFilter)
    {
        return characterFilter switch
        {
            "IRONCLAD" => ModelDb.Character<Ironclad>().CardPool.AllCards,
            "SILENT" => ModelDb.Character<Silent>().CardPool.AllCards,
            "REGENT" => ModelDb.Character<Regent>().CardPool.AllCards,
            "NECROBINDER" => ModelDb.Character<Necrobinder>().CardPool.AllCards,
            "DEFECT" => ModelDb.Character<Defect>().CardPool.AllCards,
            "SHIN_GETTER" => ModelDb.CardPool<ShinGetterCardPool>().AllCards,
            "-" => GetNonCharacterCards(),
            _ => Enumerable.Empty<CardModel>(),
        };
    }

    private static IEnumerable<CardModel> GetNonCharacterCards()
    {
        var characterPools = ModelDb.AllCharacters
            .Select(c => c.CardPool)
            .ToHashSet();
        return ModelDb.AllCards.Where(c => !characterPools.Contains(c.Pool));
    }

    private static bool MatchesIdFilter(CardModel card, string? idFilterPattern)
    {
        if (string.IsNullOrWhiteSpace(idFilterPattern) || idFilterPattern == "-")
            return true;

        if (!idFilterPattern.Contains('*'))
            return card.Id.Entry.Contains(idFilterPattern, StringComparison.OrdinalIgnoreCase);

        return Regex.IsMatch(
            card.Id.Entry,
            WildcardToRegex(idFilterPattern),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string WildcardToRegex(string pattern)
    {
        return "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
    }

    private static string GetGodotProcessDirectory()
    {
        var executablePath = OS.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
            return ProjectSettings.GlobalizePath("user://");

        var normalizedPath = NormalizePathSeparators(executablePath);
        return Path.GetDirectoryName(Path.GetFullPath(normalizedPath)) ??
               ProjectSettings.GlobalizePath("user://");
    }

    private static string NormalizePathSeparators(string path)
    {
        return path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
    }

    private static async Task<bool> TryCaptureAsync(
        SceneTree tree,
        CardModel card,
        string absolutePath,
        float scale,
        Action<string>? log,
        string fileLabel)
    {
        var host = new Control
        {
            Name = "ShinGetterCardPngExportHost",
            Position = new(-5000f, -5000f),
        };

        var ok = false;
        try
        {
            var built = BuildCaptureViewport(card, scale);
            host.AddChild(built.Viewport);
            NGame.Instance!.AddChild(host);

            await WaitFrames(tree, FramesAfterHostAdded);

            if (GodotObject.IsInstanceValid(built.Card))
            {
                ShinGetterCardFramePatch.BeginDefaultTintOverride();
                try
                {
                    built.Card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                    if (built.Card.Model is { IsUpgraded: true })
                        built.Card.ShowUpgradePreview();
                }
                finally
                {
                    ShinGetterCardFramePatch.EndDefaultTintOverride();
                }
            }

            await WaitFrames(tree, FramesAfterVisualRefresh);

            if (GodotObject.IsInstanceValid(built.Viewport))
            {
                built.Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                await WaitFrames(tree, FramesAfterRenderOnce);

                var texture = built.Viewport.GetTexture();
                using var imageFromTexture = texture?.GetImage();
                using var image = imageFromTexture?.Duplicate() as Image;
                if (image != null)
                {
                    var saveError = image.SavePng(absolutePath);
                    ok = saveError == Error.Ok;
                    if (ok)
                        await WaitFrames(tree, FramesAfterSaveBeforeTeardown);
                    else
                        log?.Invoke($"{fileLabel}: SavePng failed ({saveError}, code {(int)saveError}).");
                }
                else
                {
                    log?.Invoke($"{fileLabel}: viewport image was null.");
                }
            }
            else
            {
                log?.Invoke($"{fileLabel}: viewport was freed before capture.");
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"{fileLabel}: {ex.Message}");
            ok = false;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(host))
                DisposeExportHost(host);
            await WaitFrames(tree, FramesAfterTeardown);
        }

        return ok;
    }

    private static BuiltCaptureViewport BuildCaptureViewport(CardModel card, float scale)
    {
        var contentSize = ComputePaddedCardViewportSize(scale);
        var frame = Mathf.RoundToInt(ExportViewportFramePad);
        var viewportSize = new Vector2I(contentSize.X + frame * 2, contentSize.Y + frame * 2);

        var viewport = new SubViewport
        {
            Name = "ShinGetterCardPngExportViewport",
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = viewportSize,
        };

        var root = new Control
        {
            Name = "CardExportRoot",
            CustomMinimumSize = new(viewportSize.X, viewportSize.Y),
            Size = new(viewportSize.X, viewportSize.Y),
        };

        var nCard = InstantiateExportCard(card);
        nCard.Scale = Vector2.One * scale;

        var minLocal = CardExportContentMinInNCardLocal();
        nCard.Position = new(
            Mathf.Round(frame - minLocal.X * scale),
            Mathf.Round(frame - minLocal.Y * scale));

        root.AddChild(nCard);
        viewport.AddChild(root);

        return new()
        {
            Viewport = viewport,
            Card = nCard,
        };
    }

    private static NCard InstantiateExportCard(CardModel card)
    {
        var nCard = PreloadManager.Cache.GetScene(CardScenePath)
            .Instantiate<NCard>();
        nCard.OnInstantiated();
        nCard.Model = card;
        nCard.Visibility = ModelVisibility.Visible;
        return nCard;
    }

    private static Vector2 CardExportContentMinInNCardLocal()
    {
        return new(-CardExportHalfExtentX, -CardExportHalfExtentY);
    }

    private static Vector2I ComputePaddedCardViewportSize(float scale)
    {
        return new(
            Mathf.CeilToInt(2f * CardExportHalfExtentX * scale),
            Mathf.CeilToInt(2f * CardExportHalfExtentY * scale));
    }

    private static void DisposeExportHost(Control host)
    {
        host.GetParent()?.RemoveChildSafely(host);

        var postOrder = new List<Node>();
        CollectPostOrder(host, postOrder);

        foreach (var node in postOrder.Where(GodotObject.IsInstanceValid))
            node.QueueFreeSafelyNoPool();
    }

    private static void CollectPostOrder(Node node, List<Node> result)
    {
        foreach (var child in node.GetChildren())
            CollectPostOrder(child, result);
        result.Add(node);
    }

    private static async Task WaitFrames(SceneTree tree, int count)
    {
        for (var i = 0; i < count; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static string SanitizeFilePart(string entry)
    {
        var sanitized = Path.GetInvalidFileNameChars()
            .Aggregate(entry, (current, c) => current.Replace(c, '_'));
        return string.IsNullOrWhiteSpace(sanitized) ? "card" : sanitized;
    }

    private sealed class BuiltCaptureViewport
    {
        public required SubViewport Viewport { get; init; }
        public required NCard Card { get; init; }
    }
}
