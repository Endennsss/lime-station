using Content.Client.Stylesheets.Palette;

namespace Content.Client.Stylesheets.Stylesheets;

public partial class SystemStylesheet
{
    public override ColorPalette PrimaryPalette => Content.Client._Lime.Stylesheets.LimePalettes.Primary; // Lime-Edit - общая серая палитра
    public override ColorPalette SecondaryPalette => Content.Client._Lime.Stylesheets.LimePalettes.Secondary; // Lime-Edit - нейтральные панели
    public override ColorPalette PositivePalette => Palettes.Green;
    public override ColorPalette NegativePalette => Palettes.Red;
    public override ColorPalette HighlightPalette => Content.Client._Lime.Stylesheets.LimePalettes.Highlight; // Lime-Edit - серый акцент
}
