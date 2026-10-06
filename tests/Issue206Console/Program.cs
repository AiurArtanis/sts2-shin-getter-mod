using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Services;
using ShinGetterMod.Diagnostics;

int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
ShinGetterBondConsolePlan Parse(params string[] args)
{
    Check(ShinGetterBondConsolePlan.TryParse(args, out var plan, out string error), error);
    return plan;
}
void Command(params string[] args) { var plan = Parse(args); Check(ShinGetterBondSession.TryConsole(plan, out string result), result); }
ShinGetterBondSave Load() => JsonSerializer.Deserialize<ShinGetterBondSave>(File.ReadAllText(SaveManager.Instance.GetProfileScopedPath("shin_getter_bonds.json")))!;
ShinGetterBondSession Scene(string npc, int roomIndex)
{
    var id = new ModelId("EVENT", npc);
    var room = new EventRoom { ModelId = id };
    var player = new Player();
    player.RunState.Players.Add(player);
    player.RunState.CurrentRoom = player.RunState.BaseRoom = room;
    // Different persistent history indices identify real encounters, not model IDs.
    player.RunState.MapPointHistory.Add(new());
    for (int i = 0; i <= roomIndex; i++) player.RunState.MapPointHistory[0].Add(new() { Rooms = new() { new() { ModelId = id } } });
    return new(new EventModel { Id = id, Owner = player });
}

SaveManager.Instance.Root = Path.Combine(Path.GetTempPath(), "shin-getter-issue206-" + Guid.NewGuid().ToString("N"));
Console.WriteLine("Isolated managed fixture: " + SaveManager.Instance.Root);
// Intentionally retained for inspection, never using the user's live profile.
var planStatus = Parse("ALL", "status");
Check(ShinGetterBondSession.TryConsole(planStatus, out _), "status failed on absent sidecar");
Check(!Directory.Exists(SaveManager.Instance.Root), "status wrote files");
Check(SaveManager.Instance.HistoryReads == 0, "status imported history");
var legacyHistory = new RunHistory { StartTime = 50 };
legacyHistory.MapPointHistory.Add(new() { new() { Rooms = new() {
    new() { ModelId = new("EVENT", "PAEL") }, new() { ModelId = new("EVENT", "TANX") } } } });
SaveManager.Instance.Histories.Add(legacyHistory);
foreach (string[] bad in new[]
{
    Array.Empty<string>(), new[] { "OROBAS" }, new[] { "unknown", "clear" }, new[] { "ALL", "clear", "extra" },
    new[] { "TANX", "unlock", "RYOMA", "-1" }, new[] { "TANX", "unlock", "RYOMA", "4" },
    new[] { "TANX", "unlock", "RYOMA", "2.5" }, new[] { "TANX", "unlock", "RYOMA", "9999999999" },
    new[] { "NEOW", "unlock", "BENKEI", "1" }, new[] { "NEOW", "unlock", "return" },
    new[] { "TANX", "unlock", "win", "2" }, new[] { "TANX", "unlock", "chat", "0" },
    new[] { "ALL", "unlock", "win", "2" }, new[] { "ALL", "nonsense" },
}) Check(!ShinGetterBondConsolePlan.TryParse(bad, out _, out _), "accepted invalid args: " + string.Join(' ', bad));

// Every NPC/pilot/stage: contiguous progression, next scene, no fourth stage.
foreach (string npc in ShinGetterDialogueCatalog.BondNpcs)
foreach (string driver in ShinGetterDialogueCatalog.Drivers)
for (int n = 0; n <= 3; n++)
{
    var save = new ShinGetterBondSave();
    Parse(npc.ToLowerInvariant(), "unlock", driver.ToLowerInvariant(), n.ToString()).Apply(save);
    Check(save.Completed.Contains(npc + "_FIRST_01"), "first prerequisite");
    for (int i = 1; i <= 3; i++) Check(save.Completed.Contains($"{npc}_{driver}_BOND_{i:00}") == (i <= n), "stage prefix");
    Check(save.DebugNextDialogues[npc] == (n < 3 ? $"{npc}_{driver}_BOND_{n + 1:00}" : ShinGetterBondConsolePlan.ChoicesToken), "deterministic next stage");
}
var all = new ShinGetterBondSave();
Parse("ALL", "unlock", "ALL", "3").Apply(all);
Check(all.Completed.Count == 70, "ALL must complete 7 first + 63 bond segments only");
Check(!all.Completed.Contains("NEOW_FIRST_01"), "ALL pilot stages changed Neow");
Check(all.DebugNextDialogues.Count == 7 && all.DebugNextDialogues.Values.All(v => v.EndsWith("_CHAT_01")), "ALL complete fallback");
var aliases = Parse("坦克斯", "解锁", "龙马", "2");
Check(aliases.Targets.SequenceEqual(new[] { "TANX" }), "Chinese alias");
Check(Parse("Architect", "status").Targets.Single() == "THE_ARCHITECT", "Architect alias");

Command("TANX", "unlock", "RYOMA", "2");
var pending = Load();
Check(pending.DebugNextDialogues["TANX"] == "TANX_RYOMA_BOND_03", "request not persisted");
Check(pending.LegacyAcquaintances.ContainsKey("PAEL"), "first edit discarded an unrelated legacy acquaintance");
string path = SaveManager.Instance.GetProfileScopedPath("shin_getter_bonds.json");
byte[] unchanged = File.ReadAllBytes(path);
Command("TANX", "status");
Check(unchanged.SequenceEqual(File.ReadAllBytes(path)), "status rewrote revision/file");
var scene = Scene("TANX", 0);
Check(scene.Begin(), scene.Error);
Check(scene.Encounter!.DialogueId == "TANX_RYOMA_BOND_03", "next scene not forced");
Check(!Load().DebugNextDialogues.ContainsKey("TANX"), "request consumption not atomic with snapshot");
var resume = Scene("TANX", 0);
Check(resume.Begin() && resume.Encounter!.DialogueId == scene.Encounter.DialogueId, "reload reselected scene");
Check(resume.Encounter!.Line == 0, "bad saved line");
scene.Skip();
var after = Scene("TANX", 1);
Check(after.Begin() && after.Encounter!.DialogueId == "", "force persisted beyond one encounter");

// Outcome commands bypass RNG/gates, but leave real history untouched.
foreach (string npc in ShinGetterBondConsolePlan.Npcs)
foreach (string theme in new[] { "first", "win", "loss", "chat", "return" })
{
    if (npc == "NEOW" && theme == "return") continue;
    Command(npc, "unlock", theme);
    var themed = Scene(npc, 2);
    Check(themed.Begin(), themed.Error);
    Check(themed.Encounter!.DialogueId == npc + "_" + theme.ToUpperInvariant() + "_01", "forced theme failed");
    Check(themed.Encounter.ResultRun == 0, "fake history result was recorded");
    themed.Skip();
}
Command("NEOW", "unlock", "win", "3");
Check(Load().DebugNextDialogues["NEOW"] == "NEOW_WIN_RETURN_01", "Neow variant indexing");
Command("ALL", "unlock", "return");
Check(!Load().DebugNextDialogues.ContainsKey("NEOW") || Load().DebugNextDialogues["NEOW"] == "NEOW_WIN_RETURN_01", "ALL unsupported theme clobbered Neow");

// NPC-local clearing, snapshots/cues/legacy evidence and no re-import.
Command("TANX", "unlock", "ALL", "3");
Command("OROBAS", "unlock", "BENKEI", "1");
var beforeClear = Load();
string orobas = JsonSerializer.Serialize(beforeClear.Completed.Where(x => x.StartsWith("OROBAS_")).OrderBy(x => x));
Command("TANX", "clear");
var cleared = Load();
Check(!cleared.Completed.Any(x => x.StartsWith("TANX_")), "clear left progress");
Check(!cleared.Encounters.Values.Any(x => x.Npc == "TANX"), "clear left snapshots/cue history");
Check(!cleared.DebugNextDialogues.ContainsKey("TANX") && !cleared.LegacyAcquaintances.ContainsKey("TANX"), "clear left override/legacy");
Check(orobas == JsonSerializer.Serialize(cleared.Completed.Where(x => x.StartsWith("OROBAS_")).OrderBy(x => x)), "clear touched another NPC");
var firstAgain = Scene("TANX", 3);
Check(firstAgain.Begin() && firstAgain.Encounter!.DialogueId == "TANX_FIRST_01", "clear did not restore first meeting");
Check(Load().LegacyMigrationVersion == 1 && SaveManager.Instance.HistoryReads == 1, "existing sidecar reset re-imported game history");

// File lock failure leaves progress+pending byte exact; retry commits once.
firstAgain.Skip();
unchanged = File.ReadAllBytes(path);
var retryPlan = Parse("TANX", "unlock", "BENKEI", "1");
using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
    Check(!ShinGetterBondSession.TryConsole(retryPlan, out _), "ignored transaction lock");
Check(unchanged.SequenceEqual(File.ReadAllBytes(path)), "failed command modified live file");
Check(ShinGetterBondSession.TryConsole(retryPlan, out _), "retry failed");
Check(Load().Revision == JsonSerializer.Deserialize<ShinGetterBondSave>(unchanged)!.Revision + 1, "retry advanced revision more than once");

// Failed Begin does not consume pending; resume snapshot consumes exactly once.
unchanged = File.ReadAllBytes(path);
var beginRetry = Scene("TANX", 4);
using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) Check(!beginRetry.Begin(), "Begin ignored lock");
Check(unchanged.SequenceEqual(File.ReadAllBytes(path)), "failed Begin lost override");
Check(beginRetry.Begin() && beginRetry.Encounter!.DialogueId == "TANX_BENKEI_BOND_02", "retry lost deterministic scene");
Check(!Load().DebugNextDialogues.ContainsKey("TANX"), "successful Begin failed to consume override");
Check(!beginRetry.Encounter!.Completed, "opening a scene manufactured completion");

// Active profile guard, separate files, legacy schema and corrupt-input preservation.
SaveManager.Instance.CurrentProfileId = 1;
Command("OROBAS", "clear");
Check(Load().Completed.Count == 0, "profile inherited previous progress");
Check(!beginRetry.Advance(), "live session crossed profile switch");
SaveManager.Instance.CurrentProfileId = 0;
Check(Load().Completed.Contains("OROBAS_BENKEI_BOND_01"), "profile0 changed");
Command("ALL", "clear");
Check(Load().Completed.Count == 0 && Load().DebugNextDialogues.Count == 0 && Load().Encounters.Count == 0, "ALL clear incomplete");
string old = File.ReadAllText(path).Replace("  \"DebugNextDialogues\": {}", "  \"LegacyUnused\": {}", StringComparison.Ordinal);
File.WriteAllText(path, old);
Check(ShinGetterBondSession.TryConsole(planStatus, out _), "old schema-1 file unsupported");
unchanged = File.ReadAllBytes(path);
Check(ShinGetterBondSession.TryConsole(planStatus, out _), "old status failed");
Check(unchanged.SequenceEqual(File.ReadAllBytes(path)), "old status rewrote file");
File.WriteAllText(path, "{bad JSON");
unchanged = File.ReadAllBytes(path);
Check(!ShinGetterBondSession.TryConsole(Parse("ALL", "clear"), out _), "corrupt file overwritten");
Check(unchanged.SequenceEqual(File.ReadAllBytes(path)), "corrupt file changed");
File.WriteAllText(path, JsonSerializer.Serialize(new ShinGetterBondSave()));
var command = new ShinGetterDialogueConsoleCmd();
Check(command.CmdName == "sgd" && !command.IsNetworked && command.DebugOnly, "command registration flags");
var local = new MegaCrit.Sts2.Core.Entities.Players.Player();
local.RunState.Players.Add(local);
RunManager.Instance.State = local.RunState;
Check(command.Process(local, new[] { "TANX", "unlock", "RYOMA", "2" }).success, "standard solo rejected");
unchanged = File.ReadAllBytes(path);
ShinGetterMod.Nodes.Events.NShinGetterBondDialogue.IsOpen = true;
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "editing during active conversation");
Check(command.Process(local, new[] { "ALL", "status" }).success, "readonly status during conversation");
ShinGetterMod.Nodes.Events.NShinGetterBondDialogue.IsOpen = false;
local.RunState.Players.Add(new());
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "multiplayer progress edit");
Check(command.Process(local, new[] { "ALL", "status" }).success, "multiplayer readonly status");
local.RunState.Players.RemoveAt(1);
local.RunState.GameMode = GameMode.Daily;
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "daily mode edit");
local.RunState.GameMode = GameMode.Standard;
local.RunState.Modifiers.Add("custom");
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "custom mode edit");
local.RunState.Modifiers.Clear();
local.Character = new object();
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "other character edit");
local.Character = new ShinGetterMod.Models.Characters.ShinGetter();
RunManager.Instance.DailyTime = DateTimeOffset.UtcNow;
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "dated daily mode edit");
RunManager.Instance.DailyTime = null;
RunManager.Instance.ShouldSave = false;
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "unsaved run edit");
RunManager.Instance.ShouldSave = true;
RunManager.Instance.State = null;
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "missing run state edit");
RunManager.Instance.State = local.RunState;
MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn = true;
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "test mode edit");
MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn = false;
MegaCrit.Sts2.Core.Helpers.NonInteractiveMode.IsActive = true;
Check(!command.Process(local, new[] { "ALL", "clear" }).success, "non-interactive edit");
MegaCrit.Sts2.Core.Helpers.NonInteractiveMode.IsActive = false;
Check(unchanged.SequenceEqual(File.ReadAllBytes(path)), "blocked edits changed file");
Check(command.GetArgumentCompletions(null, new[] { "TANX", "unlock", "龙马", "" }).Candidates.SequenceEqual(new[] { "0", "1", "2", "3" }), "Chinese pilot completion");
Check(command.GetArgumentCompletions(null, new[] { "NEOW", "unlock", "win", "" }).Candidates.Count == 3, "Neow variant completion");
Check(command.GetArgumentCompletions(null, new[] { "TANX", "unlock", "win", "" }).Candidates.SequenceEqual(new[] { "1" }), "NPC variant completion");
RunManager.Instance.IsInProgress = false;
Check(command.Process(null, new[] { "ALL", "clear" }).success, "main menu profile edit");
SaveManager.Instance.CurrentProfileId = 2;
Command("TANX", "clear");
var firstReset = Load();
Check(firstReset.LegacyAcquaintances.ContainsKey("PAEL") && !firstReset.LegacyAcquaintances.ContainsKey("TANX"), "first reset changed an unrelated legacy acquaintance");
int historyReads = SaveManager.Instance.HistoryReads;
SaveManager.Instance.CurrentProfileId = 3;
Check(command.Process(null, new[] { "ALL", "status" }).success, "new profile query failed");
Check(!File.Exists(SaveManager.Instance.GetProfileScopedPath("shin_getter_bonds.json")) && historyReads == SaveManager.Instance.HistoryReads, "readonly status migrated an empty profile");
var invalid = new ShinGetterBondSave();
invalid.DebugNextDialogues["TANX"] = "OROBAS_FIRST_01";
try { ShinGetterBondConsolePlan.ValidatePending(invalid); Check(false, "cross-NPC pending accepted"); } catch (InvalidDataException) { Check(true, "rejected"); }
invalid.DebugNextDialogues["TANX"] = ShinGetterBondConsolePlan.ChoicesToken;
try { ShinGetterBondConsolePlan.ValidatePending(invalid); Check(false, "invalid choices accepted"); } catch (InvalidDataException) { Check(true, "rejected"); }
invalid.DebugNextDialogues["TANX"] = "TANX_RYOMA_BOND_03";
try { ShinGetterBondConsolePlan.ValidatePending(invalid); Check(false, "skipped prerequisites accepted"); } catch (InvalidDataException) { Check(true, "rejected"); }
// Exercise the actual Harmony prefix, not just the direct command/parser path.
var prefix = typeof(ShinGetterMod.Patches.ShinGetterConsoleCommandPatch).GetMethod("Prefix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
(bool Original, MegaCrit.Sts2.Core.DevConsole.CmdResult Result) InvokePrefix(string name, string[] args)
{
    object?[] call = { null, name, args, new MegaCrit.Sts2.Core.DevConsole.CmdResult(false, "untouched") };
    bool original = (bool)prefix.Invoke(null, call)!;
    return (original, (MegaCrit.Sts2.Core.DevConsole.CmdResult)call[3]!);
}
foreach (string input in new[] { "sgd TANX status", "sgd  TANX  status", "  sgd   TANX    status   ", "sgd ALL status   " })
{
    string[] nativeTokens = input.Trim().Split(' '); // Official109 public tokenizer boundary, fixture only.
    var response = InvokePrefix(nativeTokens[0], nativeTokens.Skip(1).ToArray());
    Check(!response.Original && response.Result.success, "prefix whitespace regression: " + input + ": " + response.Result.msg);
}
var padded = InvokePrefix("SGD", new[] { "", " \tTANX\t ", " ", "\u3000status\u3000", "\t" });
Check(!padded.Original && padded.Result.success, "prefix padded arguments");
foreach (string[] inputArgs in new[] { new[] { "", "INVALID_NPC", "", "status" }, new[] { "TANX", "bad_operation" }, new[] { "", "", " " } })
    Check(!InvokePrefix("sgd", inputArgs).Result.success, "normalization accepted invalid command");
foreach (string name in new[] { "sgs", "chunibyo", "shin_getter_add_cards", "stoner_sunshine_rate" })
{
    string[] originalArgs = { "", "\"name with spaces\"", " ", "\targ\t" };
    var response = InvokePrefix(name, originalArgs);
    Check(!response.Original && ReferenceEquals(OtherCommandCapture.Args, originalArgs), name + " arguments were rewritten");
}
foreach (string name in new[] { "export_cards", "event", "not_a_command" })
{
    string[] originalArgs = { "\"C:/target path\"", "", " tail " };
    var response = InvokePrefix(name, originalArgs);
    Check(response.Original && response.Result.msg == "untouched" && originalArgs[0] == "\"C:/target path\"", name + " passthrough changed");
}
Check(!File.Exists(SaveManager.Instance.GetProfileScopedPath("shin_getter_bonds.json")), "prefix status/error tests wrote a file");
var spacedUnlock = InvokePrefix("sgd", new[] { "", "TANX", "", "unlock", "", "RYOMA", "", "2", "" });
Check(!spacedUnlock.Original && spacedUnlock.Result.success, "spaced unlock failed");
Check(Load().DebugNextDialogues["TANX"] == "TANX_RYOMA_BOND_03", "spaced unlock changed its semantics");
byte[] beforeBadStage = File.ReadAllBytes(SaveManager.Instance.GetProfileScopedPath("shin_getter_bonds.json"));
var badStage = InvokePrefix("sgd", new[] { "", "TANX", "unlock", "", "RYOMA", "4", "" });
Check(!badStage.Result.success && beforeBadStage.SequenceEqual(File.ReadAllBytes(SaveManager.Instance.GetProfileScopedPath("shin_getter_bonds.json"))), "spaced invalid stage changed progress");
var spacedClear = InvokePrefix("sgd", new[] { "", "TANX", "", "clear", "" });
Check(!spacedClear.Original && spacedClear.Result.success && !Load().Completed.Any(id => id.StartsWith("TANX_", StringComparison.Ordinal)), "spaced clear failed");
Console.WriteLine($"PASS: {checks} managed assertions, linked production source; NOT Godot/game acceptance.");
