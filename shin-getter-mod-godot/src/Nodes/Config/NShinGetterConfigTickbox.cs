#nullable enable
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterConfigTickbox : NTickbox
{
    public bool InitialIsTicked { get; set; } = true;

    public override void _Ready()
    {
        ConnectSignals();
        IsTicked = InitialIsTicked;
    }
}
