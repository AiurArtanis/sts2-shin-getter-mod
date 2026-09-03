using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.Security;

namespace Sts2HeadlessTestBridge.State;

public sealed record SnapshotCapture(
    Dictionary<string, object?> Snapshot,
    string SnapshotId,
    string CanonicalSha256,
    string? Path);

/// <summary>
/// Collects immutable DTOs from the game on the Godot main thread. The native
/// NetFullCombatState oracle remains separate from the bridge semantic layer.
/// </summary>
public sealed class SnapshotBuilder(
    BridgeConfiguration configuration,
    string processEpoch,
    Func<string> gameVersion,
    Func<string?> gameCommit,
    Func<Dictionary<string, object?>>? actionSnapshot = null,
    Func<IReadOnlyList<Dictionary<string, object?>>>? choiceSnapshot = null)
{
    private static readonly JsonSerializerOptions OracleJson = CreateOracleJson();
    private readonly StableHandleRegistry _handles = new(processEpoch);
    private readonly Func<Dictionary<string, object?>> _actionSnapshot = actionSnapshot ?? EmptyActions;
    private readonly Func<IReadOnlyList<Dictionary<string, object?>>> _choiceSnapshot = choiceSnapshot ?? EmptyChoices;

    public StableHandleRegistry Handles => _handles;

    public SnapshotCapture Capture(string purpose, bool persist = true)
    {
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        CombatState? combat = CombatManager.Instance.DebugOnlyGetState();
        _handles.Synchronize(run, run?.CurrentRoom, combat, CombatManager.Instance.CurrentCombatId);

        string? rngBefore = RngFingerprint(run);
        long revision = _handles.CommitRevision();
        string snapshotId = $"{configuration.InstanceId}:{revision}";
        string? gameChecksum = null;
        object? authoritativeCombat = null;
        string combatCompleteness = "unavailable";
        string combatProvenance = "unavailable: no active combat";

        if (run is not null && combat is not null)
        {
            NetFullCombatState oracle = NetFullCombatState.FromRun(run, null);
            uint checksum = RunManager.Instance.ChecksumTracker.GenerateChecksum(oracle);
            gameChecksum = checksum.ToString(System.Globalization.CultureInfo.InvariantCulture);
            authoritativeCombat = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["game_oracle"] = JsonSerializer.SerializeToElement(oracle, OracleJson).Clone(),
                ["checksum"] = gameChecksum,
                ["checksum_algorithm"] = "game ChecksumTracker.GenerateChecksum(NetFullCombatState)",
            };
            combatCompleteness = "complete";
            combatProvenance = "NetFullCombatState.FromRun + ChecksumTracker.GenerateChecksum";
        }

        var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = "sts2-state/v1",
            ["snapshot_id"] = snapshotId,
            ["state_revision"] = revision,
            ["identity"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["game_version"] = gameVersion(),
                ["game_commit"] = gameCommit(),
                ["adapter_id"] = "sts2-0.111",
                ["instance_id"] = configuration.InstanceId,
                ["process_epoch"] = _handles.Epochs.Process,
                ["run_epoch"] = _handles.Epochs.Run,
                ["room_epoch"] = _handles.Epochs.Room,
                ["combat_epoch"] = _handles.Epochs.Combat,
            },
            ["clock"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["wall_time"] = DateTimeOffset.UtcNow.ToString("O"),
                ["engine_frame"] = Engine.GetProcessFrames(),
                ["logical_time"] = null,
            },
            ["location"] = Location(run, combat),
            ["authoritative_combat"] = authoritativeCombat,
            ["authoritative_run"] = AuthoritativeRun(run, rngBefore),
            ["local_semantic"] = LocalSemantic(run, combat),
            ["presentation"] = new Dictionary<string, object?>(),
            ["completeness"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["authoritative_combat"] = combatCompleteness,
                ["authoritative_run"] = run is null ? "unavailable" : "partial",
                ["local_semantic"] = run is null ? "partial" : "complete",
                ["presentation"] = "unavailable",
            },
            ["provenance"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["authoritative_combat"] = combatProvenance,
                ["authoritative_run"] = run is null ? "unavailable: no active run" : "RunState public deterministic projection",
                ["local_semantic"] = "sts2-0.111 SnapshotBuilder + stable structural handles",
                ["presentation"] = "unavailable in H0 headless mode",
                ["purpose"] = purpose,
            },
            ["hashes"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["canonical_sha256"] = new string('0', 64),
                ["game_checksum"] = gameChecksum,
                ["rng_before"] = rngBefore,
                ["rng_after"] = null,
            },
        };

        string? rngAfter = RngFingerprint(run);
        ((Dictionary<string, object?>)snapshot["hashes"]!)["rng_after"] = rngAfter;
        if (!StringComparer.Ordinal.Equals(rngBefore, rngAfter))
        {
            throw new BridgeStateException(
                ErrorCodes.ObserverSideEffect,
                $"state collection changed gameplay RNG: before={rngBefore ?? "null"}, after={rngAfter ?? "null"}");
        }

        string hash = SnapshotHash(snapshot);
        ((Dictionary<string, object?>)snapshot["hashes"]!)["canonical_sha256"] = hash;
        string? path = persist ? Persist(snapshot, snapshotId) : null;
        return new SnapshotCapture(snapshot, snapshotId, hash, path);
    }

    private Dictionary<string, object?> Location(RunState? run, CombatState? combat)
    {
        PlayerCombatState? playerCombat = combat?.Players.FirstOrDefault()?.PlayerCombatState;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["in_run"] = run is not null,
            ["act"] = run is null ? null : run.CurrentActIndex + 1,
            ["floor"] = run?.ActFloor,
            ["room"] = run?.CurrentRoom?.GetType().Name,
            ["room_handle"] = run?.CurrentRoom is null ? null : _handles.IssueRoom(run.CurrentRoom),
            ["in_combat"] = CombatManager.Instance.IsInProgress && combat is not null,
            ["turn"] = playerCombat?.TurnNumber,
            ["phase"] = playerCombat?.Phase.ToString(),
            ["side"] = combat?.CurrentSide.ToString(),
        };
    }

    private static object? AuthoritativeRun(RunState? run, string? rngFingerprint)
    {
        if (run is null)
            return null;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["seed"] = run.Rng.Seed,
            ["rng_fingerprint"] = rngFingerprint,
            ["game_mode"] = run.GameMode.ToString(),
            ["ascension"] = run.AscensionLevel,
            ["current_act_index"] = run.CurrentActIndex,
            ["act_floor"] = run.ActFloor,
            ["total_floor"] = run.TotalFloor,
            ["should_save"] = RunManager.Instance.ShouldSave,
            ["players"] = run.Players.Select(player => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["character_id"] = player.Character.Id.ToString(),
                ["hp"] = player.Creature.CurrentHp,
                ["max_hp"] = player.Creature.MaxHp,
                ["gold"] = player.Gold,
                ["deck"] = player.Deck.Cards.Select(DeckCard).ToArray(),
                ["relics"] = player.Relics.Select(relic => relic.Id.ToString()).ToArray(),
                ["potions"] = player.Potions.Select(potion => potion.Id.ToString()).ToArray(),
            }).ToArray(),
        };
    }

    private Dictionary<string, object?> LocalSemantic(RunState? run, CombatState? combat)
    {
        IReadOnlyList<Player> players = run?.Players ?? Array.Empty<Player>();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["players"] = players.Select(PlayerState).ToArray(),
            ["enemies"] = (combat?.Enemies ?? Array.Empty<Creature>()).Select(CreatureState).ToArray(),
            ["actions"] = _actionSnapshot(),
            ["choices"] = _choiceSnapshot(),
            ["shin_getter"] = ShinGetterTestExtension.Capture(players),
        };
    }

    private Dictionary<string, object?> PlayerState(Player player)
    {
        PlayerCombatState? state = player.PlayerCombatState;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["handle"] = _handles.IssuePlayer(player),
            ["character_id"] = player.Character.Id.ToString(),
            ["hp"] = player.Creature.CurrentHp,
            ["max_hp"] = player.Creature.MaxHp,
            ["block"] = player.Creature.Block,
            ["gold"] = player.Gold,
            ["energy"] = state?.Energy,
            ["max_energy"] = state?.MaxEnergy,
            ["stars"] = state?.Stars,
            ["turn"] = state?.TurnNumber,
            ["phase"] = state?.Phase.ToString(),
            ["powers"] = Powers(player.Creature),
            ["piles"] = state?.AllPiles.Select(PileState).ToArray() ?? Array.Empty<object>(),
        };
    }

    private Dictionary<string, object?> CreatureState(Creature creature) => new(StringComparer.Ordinal)
    {
        ["handle"] = _handles.IssueCreature(creature),
        ["model_id"] = creature.ModelId.ToString(),
        ["combat_id"] = creature.CombatId,
        ["hp"] = creature.CurrentHp,
        ["max_hp"] = creature.MaxHp,
        ["block"] = creature.Block,
        ["alive"] = creature.IsAlive,
        ["hittable"] = creature.IsHittable,
        ["powers"] = Powers(creature),
    };

    private object PileState(CardPile pile) => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["type"] = pile.Type.ToString(),
        ["cards"] = pile.Cards.Select(CombatCard).ToArray(),
    };

    private object CombatCard(CardModel card) => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["handle"] = _handles.IssueCombatCard(card),
        ["model_id"] = card.Id.ToString(),
        ["upgrade_level"] = card.CurrentUpgradeLevel,
        ["energy_cost_canonical"] = card.EnergyCost.Canonical,
        ["energy_cost_local"] = card.EnergyCost.GetWithModifiers(CostModifiers.Local),
        ["costs_x"] = card.EnergyCost.CostsX,
        ["target_type"] = card.TargetType.ToString(),
    };

    private static object DeckCard(CardModel card) => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["model_id"] = card.Id.ToString(),
        ["upgrade_level"] = card.CurrentUpgradeLevel,
        ["energy_cost_canonical"] = card.EnergyCost.Canonical,
        ["costs_x"] = card.EnergyCost.CostsX,
    };

    private static object[] Powers(Creature creature) => creature.Powers
        .OrderBy(power => power.Id.ToString(), StringComparer.Ordinal)
        .Select(power => (object)new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["model_id"] = power.Id.ToString(),
            ["amount"] = power.Amount,
        })
        .ToArray();

    private static string? RngFingerprint(RunState? run)
    {
        if (run is null)
            return null;
        JsonElement rng = JsonSerializer.SerializeToElement(run.Rng.ToSerializable(), OracleJson);
        return Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Serialize(rng)));
    }

    private static string SnapshotHash(Dictionary<string, object?> snapshot)
    {
        JsonElement element = JsonSerializer.SerializeToElement(snapshot);
        return Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Serialize(element)));
    }

    private string Persist(Dictionary<string, object?> snapshot, string snapshotId)
    {
        string directory = Path.GetFullPath(Path.Combine(configuration.OutputRoot, "snapshots"));
        string root = Path.GetFullPath(configuration.OutputRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new BridgeStateException(ErrorCodes.IsolationBreach, "snapshot path escaped the configured output root");
        Directory.CreateDirectory(directory);
        string safeName = snapshotId.Replace(':', '-') + ".json";
        string target = Path.Combine(directory, safeName);
        string temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
        byte[] payload = CanonicalJson.Serialize(JsonSerializer.SerializeToElement(snapshot));
        File.WriteAllBytes(temporary, payload);
        File.Move(temporary, target, overwrite: true);
        return target;
    }

    private static Dictionary<string, object?> EmptyActions() => new(StringComparer.Ordinal)
    {
        ["running"] = null,
        ["pending"] = Array.Empty<object>(),
        ["queue_empty"] = !RunManager.Instance.IsInProgress || RunManager.Instance.ActionQueueSet.IsEmpty,
        ["details_complete"] = false,
        ["incomplete_reason"] = "action observer not attached",
    };

    private static IReadOnlyList<Dictionary<string, object?>> EmptyChoices() => Array.Empty<Dictionary<string, object?>>();

    private static JsonSerializerOptions CreateOracleJson()
    {
        var options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
