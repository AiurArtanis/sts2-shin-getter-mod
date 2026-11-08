using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Cards;

// Offline CLR/native-109 contract checks. No SaveManager setup, file-save mutation,
// game process, Godot scene, map traversal, or claimed production Continue acceptance.
var mod=typeof(ShinGetterMod.Entry).Assembly;
if (args.Contains("--inspect-interaction-api"))
{
 foreach (var name in new[]{"FromChooseACardScreen","FromSimpleGrid"})
 {
  var method=AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CardSelectCmd),name);
  var machine=method.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
  foreach(var instruction in PatchProcessor.GetCurrentInstructions(AccessTools.Method(machine,"MoveNext")))
   if(instruction.operand is MethodInfo call&&call.Name.Contains("CardsSelected"))Console.WriteLine(name+" -> "+call.DeclaringType!.FullName+" / "+call);
 }
 foreach(var typeName in new[]{"NChooseACardSelectionScreen","NSimpleCardSelectScreen"})
 {
  var type=typeof(RunManager).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection."+typeName,true)!;
  Console.WriteLine(typeName+" reflection -> "+AccessTools.Method(type,"CardsSelected"));
 }
 return;
}
if (args.Contains("--inspect-save-api"))
{
 foreach (var method in typeof(RunManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name.Contains("Saved") || m.Name.Contains("Single")))
  Console.WriteLine(method.ToString());
 return;
}
var state=mod.GetType("ShinGetterMod.Services.ShinGetterPlayerEventState",true)!;
var service=mod.GetType("ShinGetterMod.Events.ShinGetterEventInvasionService",true)!;
var verification=mod.GetType("ShinGetterMod.Events.ShinGetterEventSaveVerification",true)!;
const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
object? Call(Type t,string name,params object?[] values)=>t.GetMethods(Static).Single(m=>m.Name==name&&m.GetParameters().Length==values.Length).Invoke(null,values);
void Field(object target,Type declared,string name,object? value)=>declared.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
void Prop(object target,Type declared,string name,object? value)=>declared.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
bool Rejected(Action action){try{action();return false;}catch(TargetInvocationException ex)when(ex.InnerException is JsonException){return true;}}

const string legacy="{\"Version\":1,\"Initialized\":true,\"Enabled\":true,\"Seed\":\"fixture\",\"Visits\":{\"unknown-old-visit\":{\"CompletedRoute\":\"\",\"Target\":\"\",\"Candidates\":[],\"keep_old_key\":17}},\"keep_root_key\":\"retained\"}";
var decoded=Call(state,"Decode",legacy)!;
string roundtrip=(string)Call(state,"Encode",decoded)!;
Require(!roundtrip.Contains("RoomCheckpoint"),"legacy acquired a restore stamp");
Require(roundtrip.Contains("unknown-old-visit")&&roundtrip.Contains("keep_old_key")&&roundtrip.Contains("keep_root_key"),"unknown old data was erased");
foreach(string stamp in new[]{"{\"Version\":2,\"RngCounter\":3}","{\"Version\":1,\"RngCounter\":-1}","{\"Version\":1,\"RngCounter\":100001}","{\"RngCounter\":3}"}){
 var n=JsonNode.Parse(legacy)!;n["Visits"]!["unknown-old-visit"]!["RoomCheckpoint"]=JsonNode.Parse(stamp);
 Require(Rejected(()=>Call(state,"Decode",n.ToJsonString())),"unsupported/damaged restore stamp admitted");
}
Console.WriteLine("legacy absent-stamp / unknown old fields and visits / future or damaged stamp rejection PASS");

var harmony=new Harmony("issue238.save-boundary.offline");
foreach(var t in mod.GetTypes().Where(t=>t.Name.StartsWith("ShinGetterEventState")&&t.GetCustomAttribute<HarmonyPatch>()!=null))new PatchClassProcessor(harmony,t).Patch();
Call(state,"VerifyJsonMetadata");
SerializablePlayer SavedPlayer()=>new(){NetId=1,CharacterId=new("CHARACTER","SHIN_GETTER"),CurrentHp=72,MaxHp=72,Gold=99,Rng=new(),Odds=new(),RelicGrabBag=new(),ExtraFields=new(),UnlockState=new()};
var serialized=SavedPlayer();Call(state,"SetWire",serialized,legacy);
SerializableRun MakeRun()=>new(){Players=new(){serialized},SerializableRng=new(){Seed="fixture"},SerializableOdds=new(),SerializableSharedRelicGrabBag=new(),PreFinishedRoom=new(){RoomType=RoomType.Event,EventId=new("EVENT","AROMA_OF_CHAOS"),IsPreFinished=true},VisitedMapCoords=new(){new(1,2)},EventsSeen=new(){new("EVENT","AROMA_OF_CHAOS")},MapPointHistory=new(){new(){new(){MapPointType=MapPointType.Unknown,Rooms=new(){new(){RoomType=RoomType.Event,ModelId=new("EVENT","AROMA_OF_CHAOS")}}}}},Acts=new(){new(){Id=new("ACT","OVERGROWTH"),SerializableRooms=new(){EventIds=new(){new("EVENT","AROMA_OF_CHAOS"),new("EVENT","SAPPHIRE_SEED")},NormalEncounterIds=new(),EliteEncounterIds=new(),EventsVisited=1}}}};
string Stable(SerializableRun r)=>(string)Call(verification,"StableRunSnapshot",r)!;
var expected=MakeRun();string fingerprint=Stable(expected);
var nativeJson=JsonSerializer.Serialize(expected,JsonSerializationUtility.GetTypeInfo<SerializableRun>());
SerializableRun Fresh()=>JsonSerializer.Deserialize(nativeJson,JsonSerializationUtility.GetTypeInfo<SerializableRun>())!;
var clock=Fresh();clock.SaveTime+=200;clock.RunTime+=300;Require(Stable(clock)==fingerprint,"natural clocks rejected");
foreach(var change in new Action<SerializableRun>[] {
 r=>r.PreFinishedRoom=null,r=>r.PreFinishedRoom!.IsPreFinished=false,r=>r.PreFinishedRoom!.EventId=new("EVENT","SAPPHIRE_SEED"),
 r=>r.MapPointHistory[0].Add(new()),r=>r.Acts[0].SerializableRooms.EventsVisited++,r=>r.Acts[0].SerializableRooms.EventIds.Reverse(),
 r=>r.EventsSeen.Add(new("EVENT","SAPPHIRE_SEED")),r=>r.VisitedMapCoords.Add(new(2,2)),r=>r.SerializableRng.Counters[RunRngType.UpFront]=1,
 r=>r.Players[0].Gold++,r=>Call(state,"SetWire",r.Players[0],legacy.Replace("\"Candidates\":[]","\"Candidates\":[\"HAYATO|CARD.ONE\"]"))
}){var altered=Fresh();change(altered);Require(Stable(altered)!=fingerprint,"room/history/pool/RNG/player/journal mismatch hidden");}
Console.WriteLine("actual native complete-run fingerprint: clock-only tolerance; 11 gameplay mutations detected PASS");

// The shared service's unrelated issue#89 static initializer reads these five
// canonical relics. Supply only that catalog dependency; no pickups/hooks execute.
var catalog=(Dictionary<ModelId,AbstractModel>)typeof(ModelDb).GetField("_contentById",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
foreach(Type type in new[]{typeof(DarkstonePeriapt),typeof(DreamCatcher),typeof(HandDrill),typeof(MawBank),typeof(TheBoot)}){
 var relic=(AbstractModel)RuntimeHelpers.GetUninitializedObject(type);Field(relic,typeof(AbstractModel),"<Id>k__BackingField",ModelDb.GetId(type));catalog.Add(relic.Id,relic);
}

// Real native objects with bounded missing-inventory fixtures; only the identity gate
// and initial/completed event lifecycle execute, never an actual reward route.
T Model<T>(string entry,bool mutable=false)where T:AbstractModel{
 var x=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));Field(x,typeof(AbstractModel),"<Id>k__BackingField",new ModelId(ModelDb.GetCategory(typeof(T)),entry));Prop(x,typeof(AbstractModel),nameof(AbstractModel.IsMutable),mutable);return x;
}
var character=Model<ShinGetter>("SHIN_GETTER");
var owner=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
Field(owner,typeof(Player),"<Character>k__BackingField",character);Field(owner,typeof(Player),"<NetId>k__BackingField",1UL);
var run=(RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
var players=new List<Player>{owner};var coords=new List<MapCoord>{new(1,2)};var history=new List<List<MapPointHistoryEntry>>{new(){new(){Rooms=new(){new(){RoomType=RoomType.Event,ModelId=new("EVENT","AROMA_OF_CHAOS")}}}}};
var rooms=new List<AbstractRoom>();
Field(run,typeof(RunState),"_players",players);Field(run,typeof(RunState),"_visitedMapCoords",coords);Field(run,typeof(RunState),"_mapPointHistory",history);Field(run,typeof(RunState),"_currentRooms",rooms);Prop(run,typeof(RunState),nameof(RunState.Rng),new RunRngSet("fixture"));Field(owner,typeof(Player),"_runState",run);
Prop(RunManager.Instance,typeof(RunManager),nameof(RunManager.NetService),new NetSingleplayerGameService());
Call(state,"InitializeNewRun",owner,true);
var canonical=Model<AromaOfChaos>("AROMA_OF_CHAOS");
var live=new EventRoom(canonical);var checkpoint=new EventRoom(canonical);checkpoint.MarkPreFinished();rooms.Add(checkpoint);
Require(!live.IsPreFinished&&checkpoint.ToSerializable().IsPreFinished&&checkpoint.ToSerializable().EventId==canonical.Id,"native room snapshot mutated live marker or lost identity");
string Key()=>(string)Call(service,"FourthVisitKey",owner,checkpoint.ModelId)!;
var data=Call(state,"Get",owner)!;
var visits=(System.Collections.IDictionary)data.GetType().GetProperty("Visits")!.GetValue(data)!;
var visitType=state.GetNestedType("Visit",BindingFlags.NonPublic)!;
var cpType=state.GetNestedType("EnteredRoomCheckpoint",BindingFlags.NonPublic)!;
object Visit(int? rng,string completed=""){
 var savedVisit=Activator.CreateInstance(visitType)!;Prop(savedVisit,visitType,"CompletedRoute",completed);
 if(rng.HasValue){var c=Activator.CreateInstance(cpType)!;Prop(c,cpType,"RngCounter",rng.Value);Prop(savedVisit,visitType,"RoomCheckpoint",c);}return savedVisit;
}
bool Gate()=> (bool)Call(service,"IsRestoringFourthRoom",run,checkpoint)!;
visits[Key()]=Visit(null);Require(!Gate(),"old stamp absence relaxed native restrictions");
var v=Visit(7);visits[Key()]=v;Require(Gate(),"exact event/owner/coordinate/history/visit stamp rejected");
var cachedCards=new CardModel[]{Model<SGC_Insight>("FIXTURE_A"),Model<SGC_Insight>("FIXTURE_B"),Model<SGC_Insight>("FIXTURE_C")};
Prop(v,visitType,"Target","0:fixture-target");Prop(v,visitType,"Candidates",cachedCards.Select(c=>"HAYATO|"+c.Id).ToList());
var newWire=(string)Call(state,"Encode",data)!;var stampedSave=SavedPlayer();Call(state,"SetWire",stampedSave,newWire);
string stampedJson=JsonSerializer.Serialize(stampedSave,JsonSerializationUtility.GetTypeInfo<SerializablePlayer>());
var stampedLoaded=JsonSerializer.Deserialize(stampedJson,JsonSerializationUtility.GetTypeInfo<SerializablePlayer>())!;
Call(state,"Restore",owner,stampedLoaded);data=Call(state,"Get",owner)!;visits=(System.Collections.IDictionary)data.GetType().GetProperty("Visits")!.GetValue(data)!;v=visits[Key()]!;
Require(Gate(),"new checkpoint lost across native player JSON and actual state Restore");
Require((string)visitType.GetProperty("Target")!.GetValue(v)! == "0:fixture-target","pending target changed on native JSON/state Restore");
int visitCount=visits.Count;
coords[0]=new(2,2);Require(!Gate(),"coordinate mismatch admitted");coords[0]=new(1,2);
Field(owner,typeof(Player),"<NetId>k__BackingField",2UL);Require(!Gate(),"owner mismatch admitted");Field(owner,typeof(Player),"<NetId>k__BackingField",1UL);
history[0].Add(new(){Rooms=new(){new(){RoomType=RoomType.Event,ModelId=canonical.Id}}});Require(!Gate(),"history visit mismatch admitted");history[0].RemoveAt(1);
history[0][0].Rooms[0].ModelId=new("EVENT","SAPPHIRE_SEED");Require(!Gate(),"event mismatch admitted");history[0][0].Rooms[0].ModelId=canonical.Id;
rooms.Add(new EventRoom(canonical));Require(!Gate(),"non-base/other room admitted");rooms.RemoveAt(1);
players.Add(owner);Require(!Gate(),"multiplayer admitted");players.RemoveAt(1);
Field(character,typeof(AbstractModel),"<Id>k__BackingField",new ModelId("CHARACTER","IRONCLAD"));Require(!Gate(),"non-Getter owner admitted");Field(character,typeof(AbstractModel),"<Id>k__BackingField",new ModelId("CHARACTER","SHIN_GETTER"));
Prop(RunManager.Instance,typeof(RunManager),nameof(RunManager.NetService),DispatchProxy.Create<INetGameService,HostProxy>());Require(!Gate(),"host with one player admitted");Prop(RunManager.Instance,typeof(RunManager),nameof(RunManager.NetService),new NetSingleplayerGameService());
var ancientRoom=new EventRoom(Model<Neow>("NEOW"));ancientRoom.MarkPreFinished();rooms[0]=ancientRoom;
Require(!(bool)Call(service,"IsRestoringFourthRoom",run,ancientRoom)!,"Ancient admitted by fourth-batch gate");rooms[0]=checkpoint;
var stamped=v.GetType().GetProperty("RoomCheckpoint")!.GetValue(v)!;Prop(stamped,cpType,"Version",2);Require(!Gate(),"unknown in-memory future stamp admitted");Prop(stamped,cpType,"Version",1);
Require(visits.Count==visitCount,"failed identity gate created new visits");
Console.WriteLine("actual restore gate: exact stamp; old/non-Getter/host/multiplayer/coordinate/owner/history/event/subroom rejection PASS");

var restorePatch=mod.GetType("ShinGetterMod.Patches.ShinGetterFourthCompletedEventPatch",true)!;
new PatchClassProcessor(harmony,restorePatch).Patch();new PatchClassProcessor(harmony,mod.GetType("ShinGetterMod.Patches.ShinGetterFourthRoomRestoreHookPatch",true)!).Patch();
// Tooltip text requires the game's localization/assets runtime. Substitute only
// that noninteractive display dependency; native option and completion code runs.
harmony.Patch(AccessTools.Method(typeof(HoverTipFactory),nameof(HoverTipFactory.Static)),prefix:new HarmonyMethod(typeof(TooltipFixture),nameof(TooltipFixture.Prefix)));
harmony.Patch(AccessTools.Method(typeof(LocString),nameof(LocString.GetIfExists)),prefix:new HarmonyMethod(typeof(TooltipFixture),nameof(TooltipFixture.LocPrefix)));
harmony.Patch(AccessTools.Method(typeof(LocString),nameof(LocString.GetFormattedText)),prefix:new HarmonyMethod(typeof(TooltipFixture),nameof(TooltipFixture.FormatPrefix)));
var initial=AccessTools.Method(typeof(EventModel),"SetInitialEventState");
EventModel Mutable<T>(string entry)where T:EventModel{
 var model=Model<T>(entry,true);Prop(model,typeof(EventModel),nameof(EventModel.Owner),owner);Prop(model,typeof(EventModel),nameof(EventModel.Rng),new Rng(123,2));return model;
}
var pending=Mutable<AromaOfChaos>("AROMA_OF_CHAOS");initial.Invoke(pending,new object[]{true});Require(!pending.IsFinished&&pending.CurrentOptions.Count>0&&pending.Rng.Counter==7,"pending stamped event not interactive or RNG not restored");
bool wrongModelRejected=false;try{initial.Invoke(Mutable<SapphireSeed>("SAPPHIRE_SEED"),new object[]{true});}catch(TargetInvocationException ex)when(ex.InnerException is InvalidOperationException){wrongModelRejected=true;}
Require(wrongModelRejected,"event model mismatch bypassed native prefinished guard");
var cached=service.GetMethods(Static).Single(m=>m.Name=="CachedFourthChoices").MakeGenericMethod(typeof(CardModel));
var offered=await (Task<List<CardModel>>)cached.Invoke(null,new object[]{pending,"HAYATO",cachedCards.Reverse()})!;
Require(offered.Select(c=>c.Id).SequenceEqual(cachedCards.Select(c=>c.Id))&&pending.Rng.Counter==7,"persisted candidates rerolled/reordered or consumed event RNG");
Require(pending.Rng.NextInt()==new Rng(123,7).NextInt(),"native RNG next result diverged");
await Hook.AfterRoomEntered(run,checkpoint); // Actual patch must skip; fixture has no hook inventory.
visits[Key()]=Visit(null);bool nativeRejected=false;try{initial.Invoke(Mutable<AromaOfChaos>("AROMA_OF_CHAOS"),new object[]{true});}catch(TargetInvocationException ex)when(ex.InnerException is InvalidOperationException){nativeRejected=true;}
Require(nativeRejected,"old non-Ancient prefinished event bypassed native guard");
Console.WriteLine("actual Harmony/native initial state: pending interactive + RNG cursor / old native guard / scoped AfterRoomEntered skip PASS");

var stoneCanonical=Model<StoneOfAllTime>("STONE_OF_ALL_TIME");var stoneRoom=new EventRoom(stoneCanonical);stoneRoom.MarkPreFinished();rooms[0]=stoneRoom;history[0][0].Rooms[0].ModelId=stoneCanonical.Id;
string stoneKey=(string)Call(service,"FourthVisitKey",owner,stoneCanonical.Id)!;visits[stoneKey]=Visit(9,"BENKEI");
var stone=Mutable<StoneOfAllTime>("STONE_OF_ALL_TIME");
owner.CanRemovePotions=true;
await (Task)AccessTools.Method(typeof(StoneOfAllTime),"BeforeEventStarted").Invoke(stone,new object[]{true})!;
Require(!owner.CanRemovePotions,"native Stone initial side effect not established");
initial.Invoke(stone,new object[]{true});Require(stone.IsFinished&&owner.CanRemovePotions&&stone.CurrentOptions.Count==0&&stone.Rng.Counter==9,"completed restore failed native cleanup/potion flag/RNG");
Console.WriteLine("actual completed Stone: native BeforeEventStarted flag -> completed Finish/EnsureCleanup unlocks potions, no reward route replay PASS");

var verify=verification.GetMethod("Verify",Static)!;
foreach(string fault in new[]{"ok","no-signal","write-error","read-error","mismatch","late-write-error"}){
 bool saved=false,rollback=false,complete=false;
 Task Write(){if(fault=="write-error")throw new IOException();if(fault=="no-signal")return Task.CompletedTask;saved=true;if(fault=="late-write-error")throw new IOException();return Task.CompletedTask;}
 void Read(){if(fault=="read-error")throw new IOException();if(fault=="mismatch")throw new InvalidOperationException();}
 try{await (Task)verify.Invoke(null,new object[]{(Func<Task>)Write,(Func<bool>)(()=>saved),(Action)Read})!;complete=true;}
 catch(Exception ex)when(ex.GetType().Name=="ConfirmedWriteReadbackException"){complete=true;}
 catch{rollback=true;}
 Require(complete==saved&&rollback!=saved,"Saved transaction boundary changed: "+fault);
}
Console.WriteLine("actual verifier: Saved-before/after failure semantics six cases PASS (injected; no production disk proof)");
rooms[0] = checkpoint; history[0][0].Rooms[0].ModelId = canonical.Id;
history[0][0].MapPointType = MapPointType.Unknown;
visits[Key()] = Visit(7);
var reloadSave = MakeRun(); reloadSave.Players = new() { SavedPlayer() };
Call(state, "Capture", owner, reloadSave.Players[0]);
rooms.Clear();
await ScopedMapSaveChecks.Run(mod, harmony, run, reloadSave, checkpoint);
rooms.Add(live);
await InteractionCheckpointChecks.Run(mod, harmony, run, owner, pending);
Console.WriteLine("ALL_SAVE_BOUNDARY_CLR_CONTRACTS_PASS; REAL_AUTO_SAVE_CONTINUE_NOT_RUN");

public class HostProxy:DispatchProxy
{
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="get_Type"?NetGameType.Host:method?.ReturnType.IsValueType==true?Activator.CreateInstance(method.ReturnType):null;
}

public static class TooltipFixture
{
 public static bool Prefix(ref IHoverTip __result){__result=(IHoverTip)RuntimeHelpers.GetUninitializedObject(typeof(HoverTip));return false;}
 public static bool LocPrefix(string table,string key,ref LocString __result){__result=new LocString(table,key);return false;}
 public static bool FormatPrefix(ref string __result){__result="offline-loc-fixture";return false;}
}
