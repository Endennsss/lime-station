using Content.Client.Examine;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets.Hud;

[CommonSheetlet]
public sealed class ExamineButtonSheetlet : Sheetlet<PalettedStylesheet>
{
    // Examine button colors
    // TODO: FIX!!
    private static readonly Color ExamineButtonColorContext = Color.Transparent;
    private static readonly Color ExamineButtonColorContextHover = Content.Client._Lime.Stylesheets.LimePalettes.PanelHighlight; // Lime-Edit
    private static readonly Color ExamineButtonColorContextPressed = Content.Client._Lime.Stylesheets.LimePalettes.Primary.PressedElement; // Lime-Edit
    private static readonly Color ExamineButtonColorContextDisabled = Color.FromHex("#5A5A5A");

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var buttonContext = new StyleBoxTexture { Texture = Texture.White };

        return
        [
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .PseudoNormal()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContext),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .PseudoHovered()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextHover),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .PseudoPressed()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextPressed),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .PseudoDisabled()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextDisabled),
        ];
    }
}
