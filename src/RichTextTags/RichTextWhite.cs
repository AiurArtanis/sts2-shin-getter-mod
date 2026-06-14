using Godot;
using MegaCrit.Sts2.Core.RichTextTags;

namespace ShinGetterMod.RichTextTags;

public partial class RichTextWhite : AbstractMegaRichTextEffect
{
    protected override string Bbcode => "white";

    public override bool _ProcessCustomFX(CharFXTransform charFx)
    {
        charFx.Color = Colors.White;
        return true;
    }
}
