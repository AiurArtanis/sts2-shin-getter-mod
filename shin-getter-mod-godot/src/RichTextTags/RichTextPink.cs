using Godot;
using MegaCrit.Sts2.Core.RichTextTags;

namespace ShinGetterMod.RichTextTags;

public partial class RichTextPink : AbstractMegaRichTextEffect
{
    protected override string Bbcode => "pink";

    public override bool _ProcessCustomFX(CharFXTransform charFx)
    {
        charFx.Color = new Color("FF69B4");
        return true;
    }
}
