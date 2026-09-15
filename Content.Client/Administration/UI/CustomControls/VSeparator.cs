using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class VSeparator : PanelContainer
{
    private static readonly Color SeparatorColor = Content.Client._Lime.Stylesheets.LimePalettes.PanelBorder; // Lime-Edit

    public VSeparator(Color color)
    {
        MinSize = new Vector2(2, 5);

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color
            }
        });
    }

    public VSeparator() : this(SeparatorColor) { }
}
