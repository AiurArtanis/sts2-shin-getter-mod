using Godot;
using MegaCrit.Sts2.Core.RichTextTags;

namespace ShinGetterMod.RichTextTags;

public partial class RichTextYellow : AbstractMegaRichTextEffect
{
    protected override string Bbcode => "yellow";

    public override bool _ProcessCustomFX(CharFXTransform charFx)
    {
        charFx.Color = new Color("FFE600");
        return true;
    }
}
