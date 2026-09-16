using Content.Shared._Lime.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Depth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Owns Lime's source-local decorative glow and user preferences.</summary>
public sealed partial class LimeLampBloomSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private readonly List<LimeLampBloomOverlay> _passes = new();
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
        foreach (var pass in _passes)
        {
            _overlay.RemoveOverlay(pass);
            pass.Dispose();
        }
        _passes.Clear();

        if (!enabled)
            return;

        AddPass<FloorDepth>(int.MinValue, (int) Depth.SmallMobs);
        AddPass<WallDepth>((int) Depth.Walls, (int) Depth.WallTops);
        AddPass<FurnitureDepth>((int) Depth.Objects, (int) Depth.SmallObjects);
        AddPass<LampDepth>((int) Depth.WallMountedItems, (int) Depth.WallMountedItems);
        AddPass<MachineDepth>((int) Depth.LargeObjects, (int) Depth.LargeObjects);
        AddPass<ItemDepth>((int) Depth.Items, (int) Depth.BelowMobs);
        AddPass<MobDepth>((int) Depth.Mobs, (int) Depth.OverMobs);
        AddPass<DoorDepth>((int) Depth.Doors, (int) Depth.Overdoors);
        AddPass<EffectDepth>((int) Depth.Overdoors + 1, (int) Depth.Overlays);
    }

    /// <summary>Previews intensity without changing gameplay light parameters.</summary>
    public void PreviewBloomStrength(float strength)
    {
        _strength = float.IsFinite(strength) ? Math.Clamp(strength, 0f, 1f) : 0f;
        foreach (var pass in _passes)
            pass.Strength = _strength;
    }

    private void AddPass<T>(int minimumDepth, int maximumDepth)
    {
        var pass = new LimeDepthBloomOverlay<T>(minimumDepth, maximumDepth) { Strength = _strength };
        _passes.Add(pass);
        _overlay.AddOverlay(pass);
    }

    private sealed class FloorDepth;
    private sealed class WallDepth;
    private sealed class FurnitureDepth;
    private sealed class LampDepth;
    private sealed class MachineDepth;
    private sealed class ItemDepth;
    private sealed class MobDepth;
    private sealed class DoorDepth;
    private sealed class EffectDepth;
}
