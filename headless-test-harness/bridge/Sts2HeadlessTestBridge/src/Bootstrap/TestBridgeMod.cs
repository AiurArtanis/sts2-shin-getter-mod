using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using Sts2HeadlessTestBridge.Security;

namespace Sts2HeadlessTestBridge.Bootstrap;

[ModInitializer("Init")]
public static class TestBridgeMod
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized || System.Environment.GetEnvironmentVariable("STS2_TEST_ENABLE") != "1")
            return;
        _initialized = true;
        try
        {
            BridgeConfiguration? configuration = BridgeConfiguration.Load();
            if (configuration is null)
                return;
            configuration.DestroySecret();
            if (Engine.GetMainLoop() is not SceneTree tree)
                throw new InvalidOperationException("Godot SceneTree is not available");
            var node = new BridgeRootNode { Name = "Sts2HeadlessTestBridge" };
            tree.Root.CallDeferred(Node.MethodName.AddChild, node);
            Log.Info("Sts2HeadlessTestBridge TEST-ONLY control node scheduled");
        }
        catch (Exception exception)
        {
            Log.Error($"Sts2HeadlessTestBridge refused to initialize: {exception.Message}");
        }
    }
}
