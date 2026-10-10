using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using ShinGetterMod.Nodes.Events;
using ShinGetterMod.Patches;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
bool LegalFixtureInput(NEventOptionButton button, EventModel model) => button.Valid && button.InTree && button.Visible
    && !button.Queued && button.IsEnabled && button.MouseStops && !button.Option.IsLocked && !ShinGetterBondDialogueBridge.IsBlocking(model);
(EventModel Model, NAncientEventLayout Layout) Scenario()
{
    Callable.Deferred.Clear();
    var model = new EventModel();
    var layout = new NAncientEventLayout(model);
    layout.Buttons.AddRange(new[] { new NEventOptionButton(), new NEventOptionButton(), new NEventOptionButton() });
    return (model, layout);
}
foreach (string route in new[] { "completion", "skip", "save-failure local skip", "restored closed encounter" })
{
    var (model, layout) = Scenario();
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    Check(ShinGetterBondDialogueBridge.IsBlocking(model), route + ": reward blocked during reading");
    Check(layout.Buttons.All(b => !b.IsEnabled && !b.Visible) && layout.Content.Visible, route + ": originals suspended, native content retained");
    NShinGetterBondDialogue.Last.Return();
    Check(layout.Buttons.All(b => b.IsEnabled), route + ": EnableButton alone left originals disabled");
    Check(layout.Buttons.All(b => LegalFixtureInput(b, model)), route + ": legal-flag fixture conditions not restored");
    Check(layout.Content.Visible && layout.Resumes == 1 && !ShinGetterBondDialogueBridge.IsBlocking(model), route + ": native continuation");
    NShinGetterBondDialogue.Last.Return();
    Check(layout.Resumes == 1 && layout.Buttons.All(b => b.EnableCalls == 1), route + ": duplicate callback restored twice");
}
{
    var (model, layout) = Scenario();
    layout.Buttons[0].Disable(); // native/external disable must not be stolen by the dialogue
    layout.Buttons[1].Option.IsLocked = true;
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    NShinGetterBondDialogue.Last.Return();
    Check(!layout.Buttons[0].IsEnabled && layout.Buttons[0].EnableCalls == 0, "pre-disabled native option reenabled");
    Check(layout.Buttons[1].Option.IsLocked && !LegalFixtureInput(layout.Buttons[1], model), "locked option became selectable");
    Check(LegalFixtureInput(layout.Buttons[2], model), "remaining unlocked option not restored");
}
foreach (string invalid in new[] { "freed", "off-tree", "queued", "replaced" })
{
    var (model, layout) = Scenario();
    var button = layout.Buttons[0];
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    if (invalid == "freed") button.Valid = false;
    if (invalid == "off-tree") button.InTree = false;
    if (invalid == "queued") button.Queued = true;
    if (invalid == "replaced") { layout.Buttons.Remove(button); layout.Buttons.Add(new()); }
    NShinGetterBondDialogue.Last.Return();
    Check(button.EnableCalls == 0, invalid + ": stale snapshot node restored");
    Check(layout.Buttons[1].IsEnabled, invalid + ": valid peer not restored");
}
foreach (bool valid in new[] { false, true })
{
    var (model, layout) = Scenario();
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    layout.Valid = valid;
    layout.InTree = false;
    NShinGetterBondDialogue.Last.Return();
    Check(layout.Resumes == 0 && layout.Buttons.All(b => b.EnableCalls == 0), "invalid/off-tree layout continued");
}
{
    var (model, layout) = Scenario();
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    layout.Queued = true;
    NShinGetterBondDialogue.Last.Return();
    Check(layout.Resumes == 0 && layout.Buttons.All(b => b.EnableCalls == 0), "queued layout continued");
}
{
    var (model, layout) = Scenario();
    layout.NativeAfterResume = () => layout.Buttons[0].Disable(); // e.g. authoritative native vote/choice state
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    NShinGetterBondDialogue.Last.Return();
    Check(!layout.Buttons[0].IsEnabled, "restoration after resume overwrote native disable");
}
{
    var (model, layout) = Scenario();
    ShinGetterBondDialogueBridge.Get(model).Returned = true;
    layout.Buttons[0].Disable();
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    Check(!layout.Buttons[0].IsEnabled && layout.Buttons[0].EnableCalls == 0, "already-returned path reenables native state");
}
{
    var (model, layout) = Scenario();
    ShinGetterBondDialogueBridge.Attach(model, layout, layout.OnSetupComplete);
    var ui = NShinGetterBondDialogue.Last;
    var room = new MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom(model);
    var local = new MegaCrit.Sts2.Core.Events.EventOption();
    ui.LocalOption = local;
    var route = HarmonyLib.AccessTools.Method(typeof(ShinGetterBondProtectRewardsPatch), "Prefix");
    Check(!(bool)route.Invoke(null, new object[] { room, local })!, "local story choice leaked to native reward routing");
    Check(ui.LocalChoiceCalls == 1, "exact local story choice was not handled");
    Check(!(bool)route.Invoke(null, new object[] { room, layout.Buttons[0].Option })!, "original reward accepted during story");
    Check(ui.LocalChoiceCalls == 1, "original reward mistaken for a local story option");
    var advance = HarmonyLib.AccessTools.Method(typeof(ShinGetterBondNativeAdvancePatch), "Prefix");
    Check(!(bool)advance.Invoke(null, new object[] { layout })! && ui.AdvanceCalls == 1, "native hitbox bypassed session-owned advance");
    var replace = HarmonyLib.AccessTools.Method(typeof(ShinGetterBondReplaceAncientLinesPatch), "Prefix");
    ui.IsUpdatingNativeUi = true;
    Check((bool)replace.Invoke(null, new object[] { layout })!, "native story refresh suppressed itself");
    var focus = HarmonyLib.AccessTools.Method(typeof(ShinGetterBondNativeLineFocusPatch), "Prefix");
    Check(!(bool)focus.Invoke(null, new object[] { layout })!, "native hover inspected an empty replacement container");
    ui.IsUpdatingNativeUi = false;
    Check((bool)focus.Invoke(null, new object[] { layout })!, "normal native hover was suppressed");
    Check(!(bool)replace.Invoke(null, new object[] { layout })!, "original dialogue entered bond flow");
    ui.Return();
    Check((bool)route.Invoke(null, new object[] { room, layout.Buttons[0].Option })!, "native reward routing did not resume");
    Check((bool)advance.Invoke(null, new object[] { layout })! && ui.AdvanceCalls == 1, "native advance intercepted after return");
    Check((bool)focus.Invoke(null, new object[] { layout })!, "native hover did not resume");
}
Console.WriteLine($"PASS: {assertions} production-bridge fixture assertions; NOT native Godot/input/reward proof.");
