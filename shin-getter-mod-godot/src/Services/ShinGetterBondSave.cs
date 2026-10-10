#nullable enable
using System;
using System.Collections.Generic;

namespace ShinGetterMod.Services;

internal sealed class ShinGetterBondSave
{
    public ShinGetterBondSave() { }
    public int Schema { get; set; } = 1;
    public long Revision { get; set; }
    // Empty in pre-release/test saves. Fixed feature epoch, not the mod's current version.
    public string ProgressEpoch { get; set; } = "";
    public int LegacyMigrationVersion { get; set; }
    public Dictionary<string, long> LegacyAcquaintances { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> Completed { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> LastMetRun { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> RespondedResults { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ShinGetterBondEncounter> Encounters { get; set; } = new(StringComparer.Ordinal);
    // Optional, profile-scoped test requests. Older schema-1 files omit this field.
    // A request is consumed only in the same transaction that creates its snapshot.
    public Dictionary<string, string> DebugNextDialogues { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ShinGetterBondEncounter
{
    public ShinGetterBondEncounter() { }
    public string Npc { get; set; } = "";
    public long Run { get; set; }
    public string DialogueId { get; set; } = "";
    public string TextVersion { get; set; } = "";
    public int Line { get; set; }
    public bool Closed { get; set; }
    public bool Completed { get; set; }
    public bool WasShown { get; set; }
    public long ResultRun { get; set; }
    public HashSet<int> ConsumedCues { get; set; } = new();
    // Keep translations frozen across mod updates and language switches on resume.
    public Dictionary<string, string[]> LinesByLanguage { get; set; } = new(StringComparer.Ordinal);
}
