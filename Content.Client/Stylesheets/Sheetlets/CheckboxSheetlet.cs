using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class CheckboxSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, ICheckboxConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        ICheckboxConfig checkboxCfg = sheet;

        var uncheckedTex = sheet.GetTextureOr(checkboxCfg.CheckboxUncheckedPath, NanotrasenStylesheet.TextureRoot);
        var checkedTex = sheet.GetTextureOr(checkboxCfg.CheckboxCheckedPath, NanotrasenStylesheet.TextureRoot);

        return
        [
            // Lime added start - обесцвечиваем только иконку переключателя, сохраняя текст и состояния
            E<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Prop(TextureRect.StylePropertyShader, IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>()
                    .Index(Content.Client._Lime.Stylesheets.LimeHudSheetlet.GreyscaleShader).Instance()),
            // Lime added end
            E<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Prop(TextureRect.StylePropertyTexture, uncheckedTex),
            E<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Class(CheckBox.StyleClassCheckBoxChecked)
                .Prop(TextureRect.StylePropertyTexture, checkedTex),
            E<BoxContainer>()
                .Class(CheckBox.StyleClassCheckBox)
                .Prop(BoxContainer.StylePropertySeparation, 10),
        ];
    }
}
