#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Services;

// One authority per Player, independent of removable inventory. SerializablePlayer carries it
// in native JSON and native reconnect packets; CWTs provide lifecycle, not persistence.
internal static class ShinGetterPlayerEventState
{
    internal const string FieldName = "shin_getter_event_state_v1";
    internal const int MaxBytes = 48 * 1024;
    internal const int MaxVisits = 64;
    internal sealed class Visit
    {
        public string CompletedRoute { get; set; } = "";
        public string Target { get; set; } = "";
        public List<string> Candidates { get; set; } = new();
    }
    internal sealed class Data
    {
        public int Version { get; set; } = 1;
        public bool Initialized { get; set; }
        public bool Enabled { get; set; }
        public string Seed { get; set; } = "";
        public SortedDictionary<string, Visit> Visits { get; set; } = new(StringComparer.Ordinal);
    }
    private sealed class Holder
    {
        internal Data Value = new();
        internal IRunState? Run;
    }
    private sealed class Wire { internal string? Value; }
    private static readonly ConditionalWeakTable<Player, Holder> States = new();
    private static readonly ConditionalWeakTable<SerializablePlayer, Wire> Wires = new();
    internal static bool IsShinGetter(ModelId? id) => id?.Entry == "SHIN_GETTER";

    internal static Data Get(Player owner)
    {
        Holder holder = States.GetOrCreateValue(owner);
        if (owner.RunState is not NullRunState)
        {
            if (holder.Run != null && !ReferenceEquals(holder.Run, owner.RunState))
                holder.Value = new Data();
            holder.Run = owner.RunState;
            if (holder.Value.Seed.Length != 0 && holder.Value.Seed != owner.RunState.Rng.StringSeed)
                throw new InvalidOperationException("Shin Getter event state belongs to another run.");
            holder.Value.Seed = owner.RunState.Rng.StringSeed;
        }
        if (!holder.Value.Initialized)
        {
            // Only absent legacy state is migrated. false is a real initialized value.
            holder.Value.Enabled = owner.GetRelic<SGR_GetterFurnace>()?.EventInvasionEnabled
                ?? owner.GetRelic<SGR_EmperorsFragment>()?.EventInvasionEnabled ?? false;
            holder.Value.Initialized = true;
        }
        return holder.Value;
    }

    internal static void InitializeNewRun(Player owner, bool enabled)
    {
        States.Remove(owner);
        States.Add(owner, new Holder { Run = owner.RunState, Value = new Data
        { Initialized = true, Enabled = enabled, Seed = owner.RunState.Rng.StringSeed } });
    }

    internal static string Encode(Data state)
    {
        Validate(state);
        string value = JsonSerializer.Serialize(state);
        if (Encoding.UTF8.GetByteCount(value) > MaxBytes) throw new JsonException("Event state too large.");
        return value;
    }

    internal static Data Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) > MaxBytes)
            throw new JsonException("Missing or oversized Shin Getter event state.");
        Data state = JsonSerializer.Deserialize<Data>(value) ?? throw new JsonException("Null event state.");
        Validate(state);
        state.Visits = new SortedDictionary<string, Visit>(state.Visits, StringComparer.Ordinal);
        return state;
    }

    private static void Validate(Data state)
    {
        if (state.Version != 1 || state.Seed == null || state.Visits == null || state.Visits.Count > MaxVisits)
            throw new JsonException("Unsupported or damaged Shin Getter event state schema.");
        foreach (var pair in state.Visits)
            if (pair.Key.Length > 512 || pair.Value == null || pair.Value.Target == null
                || pair.Value.CompletedRoute == null || pair.Value.CompletedRoute.Length > 64
                || pair.Value.Candidates == null || pair.Value.Candidates.Count > 3
                || pair.Value.Candidates.Any(id => id == null || id.Length > 256))
                throw new JsonException("Damaged event visit.");
    }

    internal static string? GetWire(SerializablePlayer player) => Wires.TryGetValue(player, out Wire? wire) ? wire.Value : null;
    internal static void SetWire(SerializablePlayer player, string? value)
    {
        if (value != null) Decode(value); // Unknown/damaged data is never treated as an absent field.
        Wires.GetOrCreateValue(player).Value = value;
    }
    internal static void Capture(Player owner, SerializablePlayer result)
    {
        if (owner.Character is ShinGetter) SetWire(result, Encode(Get(owner)));
    }
    internal static void Restore(Player owner, SerializablePlayer serialized)
    {
        if (owner.Character is not ShinGetter) return;
        States.Remove(owner); // Also essential for a field-less Sync, which must not retain old state.
        string? value = GetWire(serialized);
        States.Add(owner, new Holder { Value = value == null ? new Data() : Decode(value) });
        // FromSerializable is still NullRunState. Bind and validate only on the first real run read.
    }

    internal static void AddJsonField(JsonTypeInfo info)
    {
        if (info.Type != typeof(SerializablePlayer) || info.Kind != JsonTypeInfoKind.Object
            || info.Properties.Any(p => p.Name == FieldName)) return;
        JsonPropertyInfo property = info.CreateJsonPropertyInfo(typeof(string), FieldName);
        property.Get = obj => GetWire((SerializablePlayer)obj);
        property.Set = (obj, value) => SetWire((SerializablePlayer)obj, (string?)value);
        property.ShouldSerialize = (obj, value) => IsShinGetter(((SerializablePlayer)obj).CharacterId) && value != null;
        info.Properties.Add(property);
    }

    internal static void VerifyJsonMetadata()
    {
        // Force the real nested run metadata, not unrelated default reflection options.
        _ = JsonSerializationUtility.GetTypeInfo<SerializableRun>();
        JsonTypeInfo info = JsonSerializationUtility.GetTypeInfo<SerializablePlayer>();
        if (!info.Properties.Any(p => p.Name == FieldName && p.Get != null && p.Set != null))
            throw new InvalidOperationException("SerializablePlayer metadata was cached before event-state patching; persistence unavailable.");
    }
}
