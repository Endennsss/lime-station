using Content.Shared._Lime.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Depth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Owns Lime's quarter-resolution emissive bloom and user preferences.</summary>
public sealed partial class LimeLampBloomSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    private readonly List<LimeLampBloomOverlay> _passes = new();
    private float _strength = 0.45f;
    private LimeBloomSourceCache _sourceCache = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sourceCache = new LimeBloomSourceCache(_entity);
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
        foreach (var pass in _passes)
        {
            _overlay.RemoveOverlay(pass);
            pass.Dispose();
        }
        _passes.Clear();

        if (!enabled)
            return;

        AddPass<FloorDepth>(0, int.MinValue, (int) Depth.SmallMobs);
        AddPass<StructureDepth>(1, (int) Depth.Walls, (int) Depth.LargeObjects);
        AddPass<ActorDepth>(2, (int) Depth.Items, (int) Depth.OverMobs);
        AddPass<UpperDepth>(3, (int) Depth.Doors, (int) Depth.Overlays);
    }

    /// <summary>Previews intensity without changing gameplay light parameters.</summary>
    public void PreviewBloomStrength(float strength)
    {
        _strength = float.IsFinite(strength) ? Math.Clamp(strength, 0f, 1f) : 0f;
        foreach (var pass in _passes)
            pass.Strength = _strength;
    }

    private void AddPass<T>(int groupIndex, int minimumDepth, int maximumDepth)
    {
        var pass = new LimeDepthBloomOverlay<T>(groupIndex, minimumDepth, maximumDepth, _sourceCache) { Strength = _strength };
        _passes.Add(pass);
        _overlay.AddOverlay(pass);
    }

    private sealed class FloorDepth;
    private sealed class StructureDepth;
    private sealed class ActorDepth;
    private sealed class UpperDepth;
}
