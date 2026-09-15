using System.Numerics;
using Content.Shared._Lime.Shaders.Bloom;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Draws bounded source-local halos; never copies or blurs the scene.</summary>
public sealed class LimeLampBloomOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<ShaderPrototype> HaloShader = "LimeLampHalo";
    private static readonly ProtoId<ShaderPrototype> CoreShader = "LimeLampCore";

    private readonly EntityLookupSystem _lookup;
    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly EntityQuery<PointLightComponent> _lights;
    private readonly EntityQuery<SpriteComponent> _sprites;
    private readonly HashSet<Entity<LimeLampBloomComponent>> _lamps = new();
    private readonly HashSet<Entity<LimeEmissiveBloomComponent>> _emissive = new();
    private readonly ShaderInstance _halo;
    private readonly ShaderInstance _core;

    /// <summary>Global decorative intensity, from zero to one.</summary>
    public float Strength;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public LimeLampBloomOverlay()
    {
        IoCManager.InjectDependencies(this);
        _lookup = _entity.System<EntityLookupSystem>();
        _transform = _entity.System<TransformSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _lights = _entity.GetEntityQuery<PointLightComponent>();
        _sprites = _entity.GetEntityQuery<SpriteComponent>();
        _halo = _prototype.Index(HaloShader).InstanceUnique();
        _core = _prototype.Index(CoreShader).Instance();
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.Effects;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Strength > 0f && args.Viewport.Eye != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        _lamps.Clear();
        _emissive.Clear();
        // Поиск только у камеры; коллекции переиспользуются для всех кадров.
        var bounds = args.WorldAABB.Enlarged(2f);
        _lookup.GetEntitiesIntersecting(args.MapId, bounds, _lamps);
        _lookup.GetEntitiesIntersecting(args.MapId, bounds, _emissive);
        try
        {
            foreach (var lamp in _lamps)
            {
                if (!lamp.Comp.Enabled || !_lights.TryComp(lamp, out var light) || !light.Enabled || light.Energy <= 0f)
                    continue;
                if (!_sprites.TryComp(lamp, out var sprite) || !sprite.Visible || sprite.ContainerOccluded)
                    continue;

                var texture = _sprite.GetFrame(lamp.Comp.MaskSprite, _timing.RealTime);
                var size = (Vector2) texture.Size / EyeManager.PixelsPerMeter * Vector2.Clamp(lamp.Comp.MaskScale, new Vector2(0.01f), new Vector2(2f));
                handle.SetTransform(_transform.GetWorldMatrix(lamp));
                var color = light.Color * lamp.Comp.BloomColor;
                var intensity = Strength * Math.Clamp(lamp.Comp.CoreStrength, 0f, 2f);
                DrawHalo(handle, lamp.Comp.MaskOffset, size, lamp.Comp.HaloRadius, lamp.Comp.Softness, color.WithAlpha(intensity * 0.35f));
                handle.UseShader(_core);
                handle.DrawTextureRect(texture, Box2.CenteredAround(lamp.Comp.MaskOffset, size), color.WithAlpha(intensity));
            }

            foreach (var emissive in _emissive)
            {
                if (!_sprites.TryComp(emissive, out var sprite) || !sprite.Visible || sprite.ContainerOccluded)
                    continue;
                handle.SetTransform(_transform.GetWorldMatrix(emissive));
                foreach (var key in emissive.Comp.Layers)
                {
                    if (!_sprite.TryGetLayer((emissive, sprite), key, out var layer, false) || !layer.Visible)
                        continue;
                    var layerBounds = _sprite.GetLocalBounds(layer);
                    var center = sprite.Offset + layerBounds.Center * sprite.Scale;
                    var size = layerBounds.Size * Vector2.Abs(sprite.Scale);
                    var color = sprite.Color * layer.Color * emissive.Comp.Color;
                    DrawHalo(handle, center, size, emissive.Comp.Radius, 0.5f, color.WithAlpha(Strength * Math.Clamp(emissive.Comp.Strength, 0f, 2f) * 0.25f));
                }
            }
        }
        finally
        {
            handle.UseShader(null);
            handle.SetTransform(Matrix3x2.Identity);
        }
    }

    private void DrawHalo(DrawingHandleWorld handle, Vector2 center, Vector2 coreSize, float radius, float softness, Color color)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
            return;
        // Ширина ореола ограничена в метрах, а не зависит от PointLight.Radius.
        var padding = Math.Clamp(radius * 0.12f, 0.02f, 0.35f);
        _halo.SetParameter("softness", float.IsFinite(softness) ? Math.Clamp(softness, 0f, 1f) : 0.5f);
        handle.UseShader(_halo);
        handle.DrawRect(Box2.CenteredAround(center, coreSize + new Vector2(padding * 2f)), color);
    }

    protected override void DisposeBehavior()
    {
        _halo.Dispose();
        _lamps.Clear();
        _emissive.Clear();
        base.DisposeBehavior();
    }
}
