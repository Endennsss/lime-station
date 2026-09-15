using Content.Client.Stylesheets.Palette;

namespace Content.Client._Lime.Stylesheets;

/// <summary>
/// Neutral interface colors shared by the game and system stylesheets.
/// </summary>
public static class LimePalettes
{
    // Lime-Edit - общие цвета для старых контролов и статических ссылок XAML.
    public static readonly Color PanelBackground = Color.FromHex("#282828");
    public static readonly Color PanelInset = Color.FromHex("#202020");
    public static readonly Color PanelBorder = Color.FromHex("#484848");
    public static readonly Color PanelHighlight = Color.FromHex("#383838");
    public static readonly Color IconNormal = Color.FromHex("#B0B0B0");
    public static readonly Color IconHovered = Color.FromHex("#D0D0D0");
    public static readonly Color IconPressed = Color.FromHex("#909090");
    public static readonly ColorPalette Primary = ColorPalette.FromHexBase("#606060",
        background: Color.FromHex("#303030"), text: Color.FromHex("#D8D8D8"));
    public static readonly ColorPalette Secondary = ColorPalette.FromHexBase("#505050",
        background: Color.FromHex("#282828"), text: Color.FromHex("#C8C8C8"));
    public static readonly ColorPalette Highlight = ColorPalette.FromHexBase("#888888",
        background: Color.FromHex("#383838"), text: Color.FromHex("#E5E5E5"));
}
