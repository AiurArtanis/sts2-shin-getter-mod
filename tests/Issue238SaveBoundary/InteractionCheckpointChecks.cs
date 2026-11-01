using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

internal static class InteractionCheckpointChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static async Task Run(Assembly mod, Harmony harmony, RunState run, Player owner, EventModel model)
    {
        var type = mod.GetType("ShinGetterMod.Events.ShinGetterEventInteractionCheckpoint", true)!;
        foreach (string name in new[] { "ShinGetterFourthOptionInteractionPatch", "ShinGetterFourthOptionHistoryPatch", "ShinGetterFourthSelectionPresentationPatch" })
            new PatchClassProcessor(harmony, mod.GetType("ShinGetterMod.Patches." + name, true)!).Patch();
        // No scene/selector/MarkCardAsSeen execution. Only the real native history
        // method runs; explicit discovered-card mutations below are DTO/UI fixtures.
        var source = (EventSynchronizer)RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer));
        typeof(EventSynchronizer).GetField("_playerCollection", Instance)!.SetValue(source, run);
        typeof(EventSynchronizer).GetField("_events", Instance)!.SetValue(source, new List<EventModel> { model });
        var entry = run.CurrentMapPointHistoryEntry!;
        entry.PlayerStats.Clear();
        entry.PlayerStats.Add(new() { PlayerId = owner.NetId });
        var history = entry.GetEntry(owner.NetId).EventChoices;
        owner.DiscoveredCards = Enumerable.Range(0,5).Select(i=>new ModelId("CARD", "FIXTURE_" + i)).ToList();
        EventOption Option() => new(model, () => Task.CompletedTask,
            "SHIN_GETTER_EVENT_INVASION.AROMA_OF_CHAOS.pages.INITIAL.options.HAYATO", false, false, Array.Empty<IHoverTip>());
        var nativeHistory = AccessTools.Method(typeof(EventSynchronizer), "SaveEventOptionToHistory");
        object Make(EventOption option, Func<Task> save, Action<Exception>? report = null) => Activator.CreateInstance(type, Instance,
            null, new object[] { model, source, owner, option, (Func<bool>)(()=>true), save, report ?? (_=>{}) }, null)!;
        Task Ready(object session) => (Task)type.GetProperty("HistoryReady", Instance)!.GetValue(session)!;
        IDisposable Select(object session) => (IDisposable)type.GetMethod("Select", Instance)!.Invoke(session,null)!;
        Task<int> Wrap(Task<int> native) => (Task<int>)type.GetMethod("BeforeSelectionResult", Static)!.MakeGenericMethod(typeof(int)).Invoke(null,new object[]{native})!;
        void Signal(EventOption option) => type.GetMethod("HistoryCompleted", Static)!.Invoke(null,new object[]{model,source,owner,option});
        void ThrowFailure(object session) => type.GetMethod("ThrowPresentationFailure", Instance)!.Invoke(session,null);
        var saved = new List<(int History,int Discovered)>();
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chosen = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var option = Option();
        var first = Make(option,async()=>{saved.Add((history.Count,owner.DiscoveredCards.Count));saveStarted.SetResult();await saveRelease.Task;});
        using ((IDisposable)first)
        {
            Signal(Option()); Check(!Ready(first).IsCompleted,"Same-key foreign option unblocked history");
            var signal=type.GetMethod("HistoryCompleted",Static)!;
            signal.Invoke(null,new object[]{model,RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer)),owner,option});
            signal.Invoke(null,new object[]{model,source,RuntimeHelpers.GetUninitializedObject(typeof(Player)),option});
            Check(!Ready(first).IsCompleted,"Foreign synchronizer/player unblocked history");
            ExecutionContext? captured;
            using (Select(first))
            {
                captured = ExecutionContext.Capture();
                var waiting = Wrap(chosen.Task);
                Check(saved.Count==0&&!waiting.IsCompleted,"Save ran before real native history completed");
                // Three discovered candidates model the documented native UI side effect.
                owner.DiscoveredCards.AddRange(new[]{new ModelId("CARD","HEDGEHOG_TACTIC"),new ModelId("CARD","SHED_LOAD"),new ModelId("CARD","STEEL_SPIRIT")});
                nativeHistory.Invoke(source,new object[]{owner,option});
                await saveStarted.Task;
                Check(saved.Single()==(1,8),"Native history or presented discoveries missing from checkpoint");
                chosen.SetResult(7); Check(!waiting.IsCompleted,"Selection result escaped while presentation save pending");
                saveRelease.SetResult(); Check(await waiting==7,"Native selection result changed");
            }
            ThrowFailure(first);
            ((IDisposable)first).Dispose();
            var ordinary=Task.FromResult(19);Task<int>? late=null;
            ExecutionContext.Run(captured!,_=>late=Wrap(ordinary),null);
            Check(ReferenceEquals(late,ordinary)&&saved.Count==1,"Retired captured context performed a save");
        }
        var secondOption=Option();
        using (var second=(IDisposable)Make(secondOption,()=>{saved.Add((history.Count,owner.DiscoveredCards.Count));return Task.CompletedTask;}))
        {
            nativeHistory.Invoke(source,new object[]{owner,secondOption});
            using(Select(second))Check(await Wrap(Task.FromResult(-1))==-1,"Skip result changed");
            Check(saved[^1]==(2,8),"Cached re-entry native history missing");
        }
        int failures=0,attempts=0;
        var failureOption=Option();var afterFailureChoice=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var failed=(IDisposable)Make(failureOption,()=>{attempts++;return Task.FromException(new IOException("injected presentation save"));},_=>failures++))
        {
            nativeHistory.Invoke(source,new object[]{owner,failureOption});
            using(Select(failed))
            {
                bool nativeEnded=false;
                async Task<int> NativeCommand(){int result=await Wrap(afterFailureChoice.Task);nativeEnded=true;return result;}
                var command=NativeCommand();
                Check(failures==1&&!command.IsCompleted,"Save failure swallowed or native UI abandoned");
                afterFailureChoice.SetResult(-1);await command;
                bool rejected=false;try{ThrowFailure(failed);}catch(TargetInvocationException ex)when(ex.InnerException is IOException){rejected=true;}
                Check(rejected&&nativeEnded&&attempts==1,"Failure did not reject after native cleanup, or retried save");
            }
        }
        var canceledOption=Option();var cancellation=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var canceled=(IDisposable)Make(canceledOption,()=>{saved.Add((history.Count,owner.DiscoveredCards.Count));return Task.CompletedTask;}))
        {
            nativeHistory.Invoke(source,new object[]{owner,canceledOption});
            using(Select(canceled))
            {
                var pending=Wrap(cancellation.Task);Check(saved[^1]==(4,8),"Open-screen checkpoint requires a chosen result");
                cancellation.SetCanceled();bool kept=false;try{await pending;}catch(OperationCanceledException){kept=true;}
                Check(kept,"Native screen cancellation not preserved");
            }
        }
        Check(type.GetMethod("Find",Static)!.Invoke(null,new object[]{model})==null,"Interaction registry retained exited selection");
        int failedHistorySaves=0;
        var failedHistory=Make(Option(),()=>{failedHistorySaves++;return Task.CompletedTask;});
        var original=new InvalidOperationException("native history fixture failure");
        var finalizer=mod.GetType("ShinGetterMod.Patches.ShinGetterFourthOptionInteractionPatch",true)!.GetMethod("Finalizer",Static)!;
        Check(ReferenceEquals(finalizer.Invoke(null,new object[]{original,failedHistory}),original),"Native history exception replaced");
        bool historyFailed=false;try{await Ready(failedHistory);}catch(InvalidOperationException ex)when(ReferenceEquals(ex,original)){historyFailed=true;}
        Check(historyFailed&&failedHistorySaves==0&&type.GetMethod("Find",Static)!.Invoke(null,new object[]{model})==null,"Native history failure left a session or caused persistence");
        var ordinarySource=RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer));
        nativeHistory.Invoke(ordinarySource,new object[]{owner,Option()});
        Check(history.Count==5&&saved.Count==3,"Ordinary native option history gained owned side effects");
        Console.WriteLine("actual native history postfix / same-key foreign rejection / history-before-save / presented discoveries fixture / pending-save result barrier / cached re-entry / cancellation / failure cleanup without retry / captured-context revocation PASS; NO_SCENE_OR_DISK_PROOF");
    }
}
