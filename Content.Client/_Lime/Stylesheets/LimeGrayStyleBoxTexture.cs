using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Lime.Stylesheets;

/// <summary>Обесцвечивает только декоративную текстуру панели, не её содержимое.</summary>
public sealed class LimeGrayStyleBoxTexture : StyleBoxTexture
{
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index(LimeHudSheetlet.GreyscaleShader).Instance();

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        handle.UseShader(_shader);
        try
        {
            base.DoDraw(handle, box, uiScale);
        }
        finally
        {
            handle.UseShader(null);
        }
    }
}
