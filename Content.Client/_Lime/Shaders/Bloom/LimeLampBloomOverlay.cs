using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Lime.Shaders.Bloom;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Graphics.RSI;
using Robust.Client.Utility;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Draws compact lamp halos and multi-scale bloom of emissive sprite layers.</summary>
public abstract class LimeLampBloomOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IClyde _clyde = default!;

    private static readonly ProtoId<ShaderPrototype> HaloShader = "LimeLampHalo";
    private static readonly ProtoId<ShaderPrototype> CoreShader = "LimeLampCore";

    private readonly EntityLookupSystem _lookup;
    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly EntityQuery<PointLightComponent> _lights;
    private readonly EntityQuery<SpriteComponent> _sprites;
    private readonly HashSet<Entity<LimeLampBloomComponent>> _lamps = new();
    private readonly HashSet<Entity<SpriteComponent>> _visibleSprites = new();
    private readonly EntityQuery<LimeEmissiveBloomComponent> _emissiveQuery;
    private readonly EntityQuery<LimeLampBloomComponent> _lampQuery;
    private readonly LimeEmissiveBloomPass _itemBloom;
    private readonly ShaderInstance _halo;
    private readonly ShaderInstance _core;
    private readonly int _minimumDepth;
    private readonly int _maximumDepth;

    /// <summary>Global decorative intensity, from zero to one.</summary>
    public float Strength;
    // Каждый проход рисуется после своего слоя источников, но до следующего слоя объектов.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    public LimeLampBloomOverlay(int minimumDepth, int maximumDepth)
    {
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
        _itemBloom = new LimeEmissiveBloomPass(_clyde, _prototype);
        _halo = _prototype.Index(HaloShader).InstanceUnique();
        _core = _prototype.Index(CoreShader).Instance();
        ZIndex = maximumDepth + 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (Strength <= 0f || args.Viewport.Eye == null)
            return false;
        _visibleSprites.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId,
            args.WorldAABB.Enlarged(Math.Max(2f, _itemBloom.GetPadding(args.Viewport))), _visibleSprites);
        foreach (var entry in _visibleSprites)
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
        _lamps.Clear();
        _itemBloom.Clear();
        // Поиск только у камеры; коллекции переиспользуются для всех кадров.
        var bounds = args.WorldAABB.Enlarged(Math.Max(2f, _itemBloom.GetPadding(args.Viewport)));
        _lookup.GetEntitiesIntersecting(args.MapId, bounds, _lamps);
        try
        {
            foreach (var lamp in _lamps)
            {
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
                DrawHalo(handle, lamp.Comp.MaskOffset, size, lamp.Comp.HaloRadius, lamp.Comp.Softness, color.WithAlpha(intensity * 0.35f));
                handle.UseShader(_core);
                handle.DrawTextureRect(texture, Box2.CenteredAround(lamp.Comp.MaskOffset, size), color.WithAlpha(intensity));
            }

            foreach (var entry in _visibleSprites)
            {
                var sprite = entry.Comp;
                if (!MatchesDepth(sprite) || !sprite.Visible || sprite.ContainerOccluded || _lampQuery.HasComp(entry) || HasBlockingEffect(sprite))
                    continue;
                _emissiveQuery.TryComp(entry, out var settings);
                if (settings != null)
                {
                    foreach (var key in settings.Layers)
                        if (_sprite.TryGetLayer(entry.AsNullable(), key, out var layer, false))
                            CollectItemLayer(entry, layer, args.Viewport.Eye!.Rotation, settings);
                }
                else
                {
                    // Нативные unshaded-слои включают индикаторы, энергооружие и предметы в руках.
                    foreach (var spriteLayer in sprite.AllLayers)
                        if (spriteLayer is SpriteComponent.Layer layer && layer.ShaderPrototype == SpriteSystem.UnshadedId)
                            CollectItemLayer(entry, layer, args.Viewport.Eye!.Rotation, null);
                }
            }
            if (ScreenTexture != null)
                _itemBloom.Draw(args, ScreenTexture, Strength);
        }
        finally
        {
            handle.UseShader(null);
            handle.SetTransform(Matrix3x2.Identity);
        }
    }

    private void CollectItemLayer(Entity<SpriteComponent> entry,
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
        _itemBloom.Add(texture, matrix, color, Math.Clamp(strength, 0f, 2f), Math.Clamp(radius, 0f, 2f));
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

    protected override void DisposeBehavior()
    {
        _halo.Dispose();
        _itemBloom.Dispose();
        _visibleSprites.Clear();
        _lamps.Clear();
        base.DisposeBehavior();
    }
}
