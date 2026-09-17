using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Lime.Shaders.Bloom;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Graphics.RSI;
using Robust.Client.Utility;
using Robust.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Draws compact lamp halos and multi-scale bloom of emissive sprite layers.</summary>
internal abstract class LimeLampBloomOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<ShaderPrototype> HaloShader = "LimeLampHalo";

    private readonly EntityLookupSystem _lookup;
    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly EntityQuery<PointLightComponent> _lights;
    private readonly EntityQuery<SpriteComponent> _sprites;
    private readonly LimeBloomSourceCache _sourceCache;
    private readonly EntityQuery<LimeEmissiveBloomComponent> _emissiveQuery;
    private readonly EntityQuery<LimeLampBloomComponent> _lampQuery;
    private readonly ShaderInstance _halo;
    private readonly int _minimumDepth;
    private readonly int _maximumDepth;

    /// <summary>Global decorative intensity, from zero to one.</summary>
    public float Strength;
    // Каждый проход рисуется после своего слоя источников, но до следующего слоя объектов.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public LimeLampBloomOverlay(int minimumDepth, int maximumDepth, LimeBloomSourceCache sourceCache)
    {
        _sourceCache = sourceCache;
        _minimumDepth = minimumDepth;
        _maximumDepth = maximumDepth;
        IoCManager.InjectDependencies(this);
        _lookup = _entity.System<EntityLookupSystem>();
        _transform = _entity.System<TransformSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _lights = _entity.GetEntityQuery<PointLightComponent>();
        _sprites = _entity.GetEntityQuery<SpriteComponent>();
        _emissiveQuery = _entity.GetEntityQuery<LimeEmissiveBloomComponent>();
        _lampQuery = _entity.GetEntityQuery<LimeLampBloomComponent>();
        _halo = _prototype.Index(HaloShader).InstanceUnique();
        ZIndex = maximumDepth + 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (Strength <= 0f || args.Viewport.Eye == null)
            return false;
        var visibleSprites = _sourceCache.Get(args.MapId, args.WorldAABB.Enlarged(2f));
        foreach (var entry in visibleSprites)
        {
            var sprite = entry.Comp;
            if (!MatchesDepth(sprite) || !sprite.Visible || sprite.ContainerOccluded || HasBlockingEffect(sprite))
                continue;
            if (_lampQuery.HasComp(entry) || _emissiveQuery.HasComp(entry))
                return true;
            foreach (var layer in sprite.AllLayers)
                if (layer is SpriteComponent.Layer { Visible: true, Blank: false } spriteLayer &&
                    spriteLayer.ShaderPrototype == SpriteSystem.UnshadedId)
                    return true;
        }
        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        try
        {
            foreach (var entry in _sourceCache.Sources)
            {
                if (!_lampQuery.TryComp(entry, out var bloom))
                    continue;
                var lamp = new Entity<LimeLampBloomComponent>(entry.Owner, bloom);
                if (!lamp.Comp.Enabled || !_lights.TryComp(lamp, out var light) || !light.Enabled || light.Energy <= 0f)
                    continue;
                if (!_sprites.TryComp(lamp, out var sprite) || !MatchesDepth(sprite) || !sprite.Visible || sprite.ContainerOccluded || HasBlockingEffect(sprite))
                    continue;

                // Frame0 разрешает путь относительно /Textures, как в YAML SpriteSpecifier.
                var texture = _sprite.Frame0(lamp.Comp.MaskSprite);
                var size = (Vector2) texture.Size / EyeManager.PixelsPerMeter * Vector2.Clamp(lamp.Comp.MaskScale, new Vector2(0.01f), new Vector2(2f));
                handle.SetTransform(_transform.GetWorldMatrix(lamp));
                var color = light.Color * lamp.Comp.BloomColor;
                var intensity = Strength * Math.Clamp(lamp.Comp.CoreStrength, 0f, 2f);
                DrawLocalHalos(handle, lamp.Comp.MaskOffset, size, lamp.Comp.HaloRadius,
                    lamp.Comp.Softness, color, intensity);
            }

            foreach (var entry in _sourceCache.Sources)
            {
                var sprite = entry.Comp;
                if (!MatchesDepth(sprite) || !sprite.Visible || sprite.ContainerOccluded || _lampQuery.HasComp(entry) || HasBlockingEffect(sprite))
                    continue;
                _emissiveQuery.TryComp(entry, out var settings);
                if (settings != null)
                {
                    foreach (var key in settings.Layers)
                        if (_sprite.TryGetLayer(entry.AsNullable(), key, out var layer, false))
                            DrawItemLayer(handle, entry, layer, args.Viewport.Eye!.Rotation, settings);
                }
                else
                {
                    // Нативные unshaded-слои включают индикаторы, энергооружие и предметы в руках.
                    foreach (var spriteLayer in sprite.AllLayers)
                        if (spriteLayer is SpriteComponent.Layer layer && layer.ShaderPrototype == SpriteSystem.UnshadedId)
                            DrawItemLayer(handle, entry, layer, args.Viewport.Eye!.Rotation, null);
                }
            }
        }
        finally
        {
            handle.UseShader(null);
            handle.SetTransform(Matrix3x2.Identity);
        }
    }

    private void DrawItemLayer(DrawingHandleWorld handle, Entity<SpriteComponent> entry,
        SpriteComponent.Layer layer, Angle eyeRotation, LimeEmissiveBloomComponent? settings)
    {
        if (!layer.Visible || layer.Blank || layer.CopyToShaderParameters != null)
            return;
        var strength = settings?.Strength ?? 1.3f;
        var radius = settings?.Radius ?? 1.5f;
        if (!float.IsFinite(strength) || !float.IsFinite(radius) || strength <= 0f || radius <= 0f)
            return;
        var sprite = entry.Comp;
        var rotation = _transform.GetWorldRotation(entry);
        var angle = (rotation + eyeRotation).Reduced().FlipPositive();
        var state = layer.ActualState;
        var direction = state == null ? RsiDirection.South : SpriteComponent.Layer.GetDirection(state.RsiDirections, angle);
        layer.GetLayerDrawMatrix(direction, out var layerMatrix);
        if (sprite.EnableDirectionOverride && state != null)
            direction = sprite.DirectionOverride.Convert(state.RsiDirections);
        direction = direction.OffsetRsiDir(layer.DirOffset);
        var texture = state?.GetFrame(direction, layer.AnimationFrame) ?? layer.Texture;
        if (texture == null)
            return;

        // Используем те же стратегии поворота, что и нативный рендер спрайта, без изменения его слоёв.
        var renderRotation = sprite.NoRotation ? -eyeRotation : rotation - (sprite.SnapCardinals ? angle.RoundToCardinalAngle() : Angle.Zero);
        if (sprite.GranularLayersRendering)
            renderRotation = layer.RenderingStrategy switch
            {
                LayerRenderingStrategy.Default => rotation,
                LayerRenderingStrategy.NoRotation => -eyeRotation,
                LayerRenderingStrategy.SnapToCardinals => rotation - angle.RoundToCardinalAngle(),
                _ => renderRotation
            };
        var matrix = layerMatrix * sprite.LocalMatrix * Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(entry), renderRotation);
        var color = sprite.Color * layer.Color * (settings?.Color ?? Color.White);
        var size = (Vector2) texture.Size / EyeManager.PixelsPerMeter;
        handle.SetTransform(matrix);
        DrawLocalHalos(handle, Vector2.Zero, size, Math.Clamp(radius, 0f, 2f), 0.7f,
            color, Strength * Math.Clamp(strength, 0f, 2f));
    }

    private bool HasBlockingEffect(SpriteComponent sprite)
    {
        // Выделение при наведении не меняет источник свечения; скрытность и прочие эффекты исключаем.
        foreach (var effect in _sprite.GetPostShaders(sprite))
            if (effect.Id != ContentPostShaderIds.InteractionOutline &&
                effect.Id != ContentPostShaderIds.TargetOutline &&
                effect.Id != ContentPostShaderIds.DragDropOutline)
                return true;
        return false;
    }

    private bool MatchesDepth(SpriteComponent sprite) => sprite.DrawDepth >= _minimumDepth && sprite.DrawDepth <= _maximumDepth;

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

    private void DrawLocalHalos(DrawingHandleWorld handle, Vector2 center, Vector2 coreSize, float radius,
        float softness, Color color, float intensity)
    {
        // Три локальных прямоугольника заменяют дорогой полноэкранный blur.
        DrawHalo(handle, center, coreSize, radius * 0.45f, softness, color.WithAlpha(intensity * 0.28f));
        DrawHalo(handle, center, coreSize, radius * 0.9f, softness, color.WithAlpha(intensity * 0.14f));
        DrawHalo(handle, center, coreSize, radius * 1.5f, softness, color.WithAlpha(intensity * 0.06f));
    }

    protected override void DisposeBehavior()
    {
        _halo.Dispose();
        base.DisposeBehavior();
    }
}

/// <summary>Собирает видимые источники один раз за кадр для всех depth-проходов.</summary>
internal sealed class LimeBloomSourceCache
{
    private readonly EntityLookupSystem _lookup;
    private readonly IGameTiming _timing;
    private uint _frame = uint.MaxValue;

    public readonly HashSet<Entity<SpriteComponent>> Sources = new();

    public LimeBloomSourceCache(IEntityManager entity)
    {
        _lookup = entity.System<EntityLookupSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    public HashSet<Entity<SpriteComponent>> Get(MapId mapId, Box2 bounds)
    {
        if (_frame == _timing.CurFrame)
            return Sources;

        _frame = _timing.CurFrame;
        Sources.Clear();
        _lookup.GetEntitiesIntersecting(mapId, bounds, Sources);
        return Sources;
    }
}
