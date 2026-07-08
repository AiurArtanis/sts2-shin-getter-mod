using Godot;
using MegaCrit.Sts2.Core.RichTextTags;

namespace ShinGetterMod.RichTextTags;

public partial class RichTextGetterRay : AbstractMegaRichTextEffect
{
    protected override string Bbcode => "getter_ray";

    public override bool _ProcessCustomFX(CharFXTransform charFx)
    {
        charFx.Color = new Color("44FCC5");
        return true;
    }
}
