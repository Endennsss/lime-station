using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Sheetlets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Lime.Stylesheets;

[CommonSheetlet]
public sealed class LimeHudSheetlet : Sheetlet<PalettedStylesheet>
{
    public const string BackgroundStyleClass = "LimeHudBackground";
    internal static readonly ProtoId<ShaderPrototype> GreyscaleShader = "Greyscale";

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var shader = IoCManager.Resolve<IPrototypeManager>().Index(GreyscaleShader).Instance();
        return [E<TextureRect>().Class(BackgroundStyleClass).Prop(TextureRect.StylePropertyShader, shader)];
    }
}
