using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Migrations.ProgressSaves;
using MegaCrit.Sts2.Core.Saves.Migrations.RunHistories;
using MegaCrit.Sts2.Core.Saves.Migrations.SerializableRuns;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Models.Relics;

public partial class Probe : Node
{
    private const string OfficialGameHash = "DDCEB87905B6096DEB7BE975922E24A53992453675DF26DF545B1C46B1826C5D";
    private static readonly Assembly Mod = typeof(SGP_FightingSpirit).Assembly;

    public override async void _Ready()
    {
        try
        {
            Check(Hash(typeof(CreatureCmd).Assembly) == OfficialGameHash, "Game input must be the official 0.114.0 DLL.");
            GD.Print($"Input DLL hashes: sts2={OfficialGameHash}; ShinGetterMod={Hash(Mod)}; 0Harmony={Hash(typeof(Harmony).Assembly)}");
            TestMode.IsOn = true;
            CheckMigrations();
            InstallDamagePatches();
            AssemblyInfo.Init();
            AssemblyInfo.ModMap!.Add(Mod, new Mod { path = string.Empty, state = ModLoadState.Loaded });
            ModelDb.Init(new[] { typeof(Ironclad), typeof(BigDummy), typeof(DoorsOfLightAndDark),
                typeof(VigorPower), typeof(StrengthPower), typeof(SGC_Strike), typeof(SGP_FightingSpirit),
                typeof(SGP_Indomitable), typeof(SGP_DelayDamage), typeof(SGP_DelayDamagePending), typeof(SGP_DelayDamageDue),
                typeof(SGR_ActivatedSapphire), typeof(SGR_MusashiClone) });
            ModelIdSerializationCache.Init();
            ModelDb.InitIds();
            await CheckCounter(lethal: true);
            await CheckCounter(lethal: false);
            await CheckMultihit();
            await CheckSapphire();
            await CheckIndomitablePreview();
            await CheckMusashiPreview();
            await CheckEventFaultBoundary();
            CheckTreasureVotePacket();
            GD.Print("ISSUE295 MANAGED BEHAVIOR PROBE PASS");
            GetTree().Quit(0);
        }
        catch (Exception failure)
        {
            GD.PrintErr(failure.ToString());
            GetTree().Quit(1);
        }
    }

    private static void CheckMigrations()
    {
        IMigration[] migrations =
        {
            new ProgressSaveV24ToV25(), new RunHistoryV11ToV12(), new SerializableRunV21ToV22(),
        };
        foreach (IMigration migration in migrations)
        {
            var input = JsonNode.Parse("""
                {"discovered_cards":["CARD.RAPID_SHOT","CARD.S_G_C_STRIKE"],
                 "players":[{"character_id":"CHARACTER.SHIN_GETTER",
                 "deck":[{"id":"CARD.RAPID_SHOT"},{"id":"CARD.S_G_C_ANNOTATIONS"}],
                 "shin_getter_event_state_v1":"{\"Version\":1}"}]}
                """)!.AsObject();
            input["schema_version"] = migration.FromVersion;
            var expected = input.DeepClone().AsObject();
            expected["discovered_cards"]![0] = "CARD.TWIN_SHOT";
            expected["players"]![0]!["deck"]![0]!["id"] = "CARD.TWIN_SHOT";
            expected["schema_version"] = migration.ToVersion;
            var actual = migration.Migrate(new MigratingData(input)).GetRawNode();
            Check(JsonNode.DeepEquals(expected, actual), $"Unexpected save mutation in {migration.SaveType.Name}.");
            GD.Print($"PASS {migration.SaveType.Name}: {migration.FromVersion}->{migration.ToVersion}; Getter data preserved");
        }
    }

    private static void InstallDamagePatches()
    {
        const string owner = "issue295.development-probe";
        var harmony = new Harmony(owner);
        foreach (string name in new[]
        {
            "Patches.ShinGetterFightingSpiritTimingPatch", "Patches.ShinGetterDamagePreviewPatch",
            "Models.Powers.FightingSpiritIncomingHpLossPatch", "Models.Powers.FightingSpiritVigorDamagePatch",
            "Patches.ShinGetterActivatedSapphirePatch", "Models.Relics.MusashiHatFatalDamagePatch",
        })
        {
            var type = Mod.GetType("ShinGetterMod." + name, throwOnError: true)!;
            Check(new PatchClassProcessor(harmony, type).Patch()?.Count > 0, name + " did not install.");
        }
        Check(Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m).Owners.Contains(owner)) == 4,
            "Expected four patched original methods.");
        GD.Print("PASS Harmony emitted six product patch classes on four actual 114 originals");
    }

    private static (Player Player, Creature Enemy, CombatState State) CreateCombat(int enemyHp)
    {
        var character = Canonical<Ironclad>();
        // Keep the fixture independent of the game's localization and starting inventory.
        var player = (Player)typeof(Player).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(c => c.GetParameters().First().ParameterType == typeof(CharacterModel))
            .Invoke(new object?[] { character, 1UL, 40, 40, 3, 0, 0, 0, new RelicGrabBag(),
                System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UnlockState)),
                null, null, null, null, null });
        var state = new CombatState();
        state.AddPlayer(player);
        player.ResetCombatState();
        var enemy = state.CreateCreature(Canonical<BigDummy>().ToMutable(), CombatSide.Enemy, null);
        state.AddCreature(enemy);
        enemy.SetCurrentHpInternal(enemyHp);
        BindTurnState(state, inProgress: false);
        return (player, enemy, state);
    }

    private static T Canonical<T>() where T : AbstractModel => ModelDb.GetById<T>(ModelDb.GetId(typeof(T)));

    private static async Task CheckCounter(bool lethal)
    {
        var (player, enemy, _) = CreateCombat(lethal ? 3 : 20);
        var owner = player.Creature;
        AddPower<SGP_FightingSpirit>(owner, 5);
        var vigor = AddPower<VigorPower>(owner, 7);
        AddPower<StrengthPower>(owner, 2);
        var sapphire = AddRelic<SGR_ActivatedSapphire>(player);
        owner.GainBlockInternal(4);
        enemy.IsAttacking = true;
        var result = (await Hit(owner, enemy, 12)).Single(r => r.Receiver == owner);
        Check(enemy.CurrentHp == (lethal ? 0 : 13) && owner.Block == 0 && owner.CurrentHp == (lethal ? 40 : 32)
            && result.UnblockedDamage == (lethal ? 0 : 8) && sapphire.DisplayAmount == (lethal ? 0 : 1) && vigor.Amount == 7,
            $"Wrong counter settlement: lethal={lethal}, enemy={enemy.CurrentHp}, player={owner.CurrentHp}, "
            + $"block={owner.Block}, loss={result.UnblockedDamage}, sapphire={sapphire.DisplayAmount}");
        GD.Print($"PASS Fighting Spirit: lethal={lethal}; block/HP/sapphire, Strength applied, Vigor unchanged");
    }

    private static async Task CheckMultihit()
    {
        var (player, enemy, state) = CreateCombat(3);
        BindTurnState(state, inProgress: true);
        AddPower<SGP_FightingSpirit>(player.Creature, 5);
        var attack = await DamageCmd.Attack(12).FromMonster(enemy.Monster!)
            .WithHitCount(3).Execute(new ThrowingPlayerChoiceContext());
        Check(attack.Results.Count() == 1 && player.Creature.CurrentHp == 40
            && enemy.IsDead && !enemy.IsAttacking && !state.ContainsCreature(enemy),
            "Counter kill did not stop remaining hits or restore the native attacker lifecycle.");
        GD.Print("PASS AttackCommand.Execute: a counter kill stops remaining hits and detaches the attacker");
    }

    private static async Task CheckSapphire()
    {
        var (player, enemy, state) = CreateCombat(100);
        var sapphire = AddRelic<SGR_ActivatedSapphire>(player);
        for (int hit = 0; hit < 3; hit++)
            await Hit(player.Creature, enemy, 4);
        for (int preview = 0; preview < 5; preview++)
            Check(Preview(state, player.Creature, enemy, 4) == 0 && sapphire.DisplayAmount == 3
                && player.Creature.CurrentHp == 28, "Fourth-hit preview consumed sapphire or changed HP.");
        await Hit(player.Creature, enemy, 4);
        await Hit(player.Creature, enemy, 4);
        Check(sapphire.DisplayAmount == 4 && player.Creature.CurrentHp == 24,
            "The fourth actual hit should be blocked and the fifth hit should land.");
        GD.Print("PASS sapphire: five previews are read-only; fourth actual hit prevented");
    }

    private static async Task CheckIndomitablePreview()
    {
        var (player, enemy, state) = CreateCombat(100);
        var power = AddPower<SGP_Indomitable>(player.Creature, 1);
        var dataField = AccessTools.Field(typeof(PowerModel), "_internalData");
        object data = dataField.GetValue(power)!;
        var pending = AccessTools.Field(data.GetType(), "pendingDamage");
        for (int preview = 0; preview < 5; preview++)
            Check(Preview(state, player.Creature, enemy, 12) == 0 && (int)pending.GetValue(data)! == 0
                && power.Amount == 1 && player.Creature.CurrentHp == 40, "Indomitable preview queued damage.");
        var previewPatch = Mod.GetType("ShinGetterMod.Patches.ShinGetterDamagePreviewPatch", true)!;
        // Force a native preview exception, then ensure the next real calculation is not a preview.
        bool threw = false;
        try { CreatureCmd.GetDamageResultsPreview(null!, state, player.Creature, 12, ValueProp.Move, enemy, Canonical<SGC_Strike>()); }
        catch (NullReferenceException) { threw = true; }
        Check(threw && !(bool)AccessTools.Property(previewPatch, "IsActive").GetValue(null)!,
            "The native error control did not throw or the preview scope leaked.");
        await Hit(player.Creature, enemy, 12);
        Check(player.Creature.CurrentHp == 40 && player.Creature.GetPower<SGP_Indomitable>() == null
            && player.Creature.GetPowerAmount<SGP_DelayDamagePending>() == 12,
            "Real settlement did not queue exactly one hit and spend Indomitable.");
        GD.Print("PASS Indomitable: five previews are read-only; native error unwinds the scope");
    }

    private static async Task CheckMusashiPreview()
    {
        var (player, enemy, state) = CreateCombat(100);
        var hat = AddRelic<SGR_MusashiClone>(player);
        BindTurnState(state, inProgress: true);
        try
        {
            Check(Preview(state, player.Creature, enemy, 50) == 0 && !hat.TriggeredThisCombat
                && player.Creature.CurrentHp == 40, "Musashi preview reserved its fatal protection.");
            Check(Preview(state, player.Creature, enemy, 10) == 10 && !hat.TriggeredThisCombat,
                "Nonfatal preview incorrectly simulated protection.");
            await Hit(player.Creature, enemy, 50);
            Check(hat.TriggeredThisCombat && player.Creature.CurrentHp == 40,
                "Musashi did not protect and spend its charge on the first actual lethal hit.");
            await Hit(player.Creature, enemy, 4);
            Check(player.Creature.CurrentHp == 36, "Following damage was incorrectly prevented.");
        }
        finally
        {
            AccessTools.Field(typeof(CombatManager), "_turnState").SetValue(CombatManager.Instance, null);
        }
        GD.Print("PASS Musashi Clone: preview is read-only; first actual lethal hit spends protection");
    }

    private static void BindTurnState(CombatState state, bool inProgress)
    {
        var turnField = AccessTools.Field(typeof(CombatManager), "_turnState");
        var turn = Activator.CreateInstance(turnField.FieldType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { state }, null)!;
        AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, inProgress);
        turnField.SetValue(CombatManager.Instance, turn);
    }

    private static async Task CheckEventFaultBoundary()
    {
        var choose = AccessTools.DeclaredMethod(typeof(EventSynchronizer), "ChooseOrFinish");
        var label = new LocString("events", "probe");
        foreach (Exception? expected in new Exception?[] { null, new InvalidOperationException("probe fault"), new OperationCanceledException() })
        {
            var model = Canonical<DoorsOfLightAndDark>().ToMutable();
            int calls = 0;
            var option = new EventOption(model, () =>
            {
                calls++;
                return expected == null ? Task.CompletedTask : Task.FromException(expected);
            }, label, label, "probe", Array.Empty<IHoverTip>());
            Exception? actual = null;
            try { await (Task)choose.Invoke(null, new object[] { model, option })!; }
            catch (Exception e) { actual = e; }
            Check(calls == 1 && actual == expected && model.IsFinished == (expected is InvalidOperationException),
                "Unexpected native event completion for success, cancellation or a fault.");
        }
        GD.Print("PASS event choice wrapper: success/cancel do not force-finish; fault finalizes and propagates");
    }

    private static void CheckTreasureVotePacket()
    {
        var (player, _, _) = CreateCombat(100);
        foreach (int? index in new int?[] { 0, null })
        foreach (int round in new[] { 0, 1 })
        {
            var original = (NetPickRelicAction)new PickRelicAction(player, index, round).ToNetAction();
            var writer = new PacketWriter();
            original.Serialize(writer);
            var reader = new PacketReader();
            reader.Reset(writer.Buffer);
            var copied = new NetPickRelicAction();
            copied.Deserialize(reader);
            var throughAction = (NetPickRelicAction)copied.ToGameAction(player).ToNetAction();
            Check(copied.relicIndex == index && copied.roundIndex == round
                && throughAction.relicIndex == index && throughAction.roundIndex == round, "Relic vote failed native round trip.");
        }
        GD.Print("PASS treasure vote wire: choices and passes keep round 0/1 through the native packet/action");
    }

    private static T AddPower<T>(Creature target, int amount) where T : PowerModel, new()
    {
        var power = (T)Canonical<T>().ToMutable();
        power.ApplyInternal(target, amount, silent: true);
        return power;
    }

    private static T AddRelic<T>(Player player) where T : RelicModel, new()
    {
        var relic = (T)Canonical<T>().ToMutable();
        player.AddRelicInternal(relic, silent: true);
        return relic;
    }

    private static Task<IEnumerable<DamageResult>> Hit(Creature player, Creature enemy, int amount) =>
        CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player, amount, ValueProp.Move | ValueProp.SkipHurtAnim, enemy);

    private static int Preview(CombatState state, Creature player, Creature enemy, int amount) =>
        CreatureCmd.GetDamageResultsPreview(state.RunState, state, player, amount, ValueProp.Move, enemy,
            Canonical<SGC_Strike>()).Sum(r => r.UnblockedDamage + r.OverkillDamage);

    private static string Hash(Assembly assembly)
    {
        string path = assembly.Location;
        if (path.Length == 0)
            path = Path.Combine(ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug"),
                assembly.GetName().Name + ".dll");
        using var input = File.OpenRead(path);
        using var pe = new PEReader(input, PEStreamOptions.LeaveOpen);
        var metadata = pe.GetMetadataReader();
        Check(metadata.GetGuid(metadata.GetModuleDefinition().Mvid) == assembly.ManifestModule.ModuleVersionId,
            "Loaded assembly differs from " + path);
        input.Position = 0;
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
            throw new InvalidOperationException(message);
    }
}
