using Content.Shared._Lime.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Owns Lime's source-local decorative glow and user preferences.</summary>
public sealed partial class LimeLampBloomSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private LimeLampBloomOverlay? _lampBloom;
    private float _strength = 0.45f;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_configuration, LimeGraphicsCVars.LightBloomStrength, PreviewBloomStrength, true);
        Subs.CVar(_configuration, LimeGraphicsCVars.LightBloomEnabled, OnEnabled, true);
    }

    public override void Shutdown()
    {
        OnEnabled(false);
        base.Shutdown();
    }

    private void OnEnabled(bool enabled)
    {
        if (_lampBloom != null)
        {
            _overlay.RemoveOverlay(_lampBloom);
            _lampBloom.Dispose();
            _lampBloom = null;
        }

        if (!enabled)
            return;

        _lampBloom = new LimeLampBloomOverlay { Strength = _strength };
        _overlay.AddOverlay(_lampBloom);
    }

    /// <summary>Previews intensity without changing gameplay light parameters.</summary>
    public void PreviewBloomStrength(float strength)
    {
        _strength = float.IsFinite(strength) ? Math.Clamp(strength, 0f, 1f) : 0f;
        if (_lampBloom != null)
            _lampBloom.Strength = _strength;
    }
}
