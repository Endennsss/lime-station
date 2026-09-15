using Content.Client._Lime.Shaders.Bloom;
using Content.Shared._Lime.Graphics;

#pragma warning disable IDE0130 // Расширение ванильной вкладки из папки Lime.
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    // Настройки свечения используют штатное сохранение, сброс и предпросмотр.
    [Dependency] private IEntityManager _entity = default!;

    private void InitializeLimeBloomOptions()
    {
        Control.AddOptionCheckBox(LimeGraphicsCVars.LightBloomEnabled, LimeBloomEnabledCheckBox);
        var strength = Control.AddOptionPercentSlider(LimeGraphicsCVars.LightBloomStrength, LimeBloomStrengthSlider);
        LimeBloomStrengthSlider.Slider.Rounded = true;
        LimeBloomStrengthSlider.Slider.RoundingDecimals = 2;
        strength.ImmediateValueChanged += PreviewLimeBloomStrength;
        LimeBloomEnabledCheckBox.OnToggled += _ => UpdateLimeBloomVisibility();
    }

    private void UpdateLimeBloomVisibility()
    {
        LimeBloomStrengthSlider.Visible = LimeBloomEnabledCheckBox.Pressed;
    }

    private void PreviewLimeBloomStrength(float strength)
    {
        _entity.System<LimeLampBloomSystem>().PreviewBloomStrength(strength);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            PreviewLimeBloomStrength(_cfg.GetCVar(LimeGraphicsCVars.LightBloomStrength));

        base.Dispose(disposing);
    }
}
