using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Lime.Stylesheets;

/// <summary>
/// Typography for Lime Station chat and speech bubbles.
/// </summary>
[CommonSheetlet]
public sealed class LimeChatSheetlet : Sheetlet<PalettedStylesheet>
{
    public const string ChatText = "LimeChatText";
    public const string SpeechText = "LimeSpeechText";

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var fonts = new NotoFontFamilyStack(ResCache, "Display");
        return
        [
            E<OutputPanel>().Class(ChatText).Font(fonts.GetFont(13)),
            E<RichTextLabel>().Class(SpeechText)
                .Font(fonts.GetFont(13))
                .Prop(Label.StylePropertyFontOutlineThickness, 0f),
            E<PanelContainer>().Class("speechBox", "emoteBox")
                .ParentOf(E<RichTextLabel>().Class(SpeechText))
                .Font(fonts.GetFont(13, FontKind.Italic)),
            E<PanelContainer>().Class("speechBox", "whisperBox")
                .ParentOf(E<RichTextLabel>().Class(SpeechText))
                .Font(fonts.GetFont(13, FontKind.Italic)),
        ];
    }
}
