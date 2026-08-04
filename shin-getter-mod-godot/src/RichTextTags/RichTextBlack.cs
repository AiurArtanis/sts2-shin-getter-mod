using Godot;
using MegaCrit.Sts2.Core.RichTextTags;

namespace ShinGetterMod.RichTextTags;

public partial class RichTextBlack : AbstractMegaRichTextEffect
{
    protected override string Bbcode => "black";

    public override bool _ProcessCustomFX(CharFXTransform charFx)
    {
        charFx.Color = new Color("000000");
        return true;
    }
}
