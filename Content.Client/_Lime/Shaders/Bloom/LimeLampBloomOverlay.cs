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
    private readonly int _groupIndex;

    /// <summary>Global decorative intensity, from zero to one.</summary>
    public float Strength;
    // Каждый проход рисуется после своего слоя источников, но до следующего слоя объектов.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public LimeLampBloomOverlay(int groupIndex, int minimumDepth, int maximumDepth, LimeBloomSourceCache sourceCache)
    {
        _groupIndex = groupIndex;
        _sourceCache = sourceCache;
        _minimumDepth = minimumDepth;
        _maximumDepth = maximumDepth;
        IoCManager.InjectDependencies(this);
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
        var visibleSprites = _sourceCache.Get(args.Viewport, args.MapId, args.WorldAABB.Enlarged(2f), _groupIndex);
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
            foreach (var entry in _sourceCache.Get(args.Viewport, args.MapId, args.WorldAABB.Enlarged(2f), _groupIndex))
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
                DrawLocalBloom(handle, texture, lamp.Comp.MaskOffset, size, lamp.Comp.HaloRadius,
                    lamp.Comp.Softness, color, intensity);
            }

            foreach (var entry in _sourceCache.Get(args.Viewport, args.MapId, args.WorldAABB.Enlarged(2f), _groupIndex))
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
        DrawLocalBloom(handle, texture, Vector2.Zero, size, Math.Clamp(radius, 0f, 2f), 0.7f,
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

    private void DrawLocalBloom(DrawingHandleWorld handle, Texture texture, Vector2 center, Vector2 coreSize,
        float radius, float softness, Color color, float intensity)
    {
        if (!float.IsFinite(radius) || !float.IsFinite(intensity) || radius <= 0f || intensity <= 0f)
            return;

        softness = float.IsFinite(softness) ? Math.Clamp(softness, 0f, 1f) : 0.5f;
        DrawBloomBand(handle, texture, center, coreSize, radius * 0.12f, color, intensity * 0.50f);
        DrawBloomBand(handle, texture, center, coreSize, radius * 0.25f, color,
            intensity * 0.32f * (0.65f + softness * 0.35f));
        DrawBloomBand(handle, texture, center, coreSize, radius * 0.45f, color,
            intensity * 0.18f * (0.35f + softness * 0.65f));
    }

    private void DrawBloomBand(DrawingHandleWorld handle, Texture texture, Vector2 center, Vector2 coreSize,
        float padding, Color color, float weight)
    {
        padding = Math.Clamp(padding, 0.02f, 0.8f);
        var expandedSize = coreSize + new Vector2(padding * 2f);
        var safeCore = Vector2.Max(coreSize, new Vector2(0.01f));
        _halo.SetParameter("core_scale", safeCore / expandedSize);
        _halo.SetParameter("sample_radius", Vector2.Min(new Vector2(padding) / safeCore * 0.85f, new Vector2(1f)));
        _halo.SetParameter("bloom_color", color);
        _halo.SetParameter("bloom_weight", weight);
        handle.UseShader(_halo);
        handle.DrawTextureRect(texture, Box2.CenteredAround(center, expandedSize), Color.White);
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
    private readonly Dictionary<IClydeViewport, ViewportSources> _viewports = new();

    public LimeBloomSourceCache(IEntityManager entity)
    {
        _lookup = entity.System<EntityLookupSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    public List<Entity<SpriteComponent>> Get(IClydeViewport viewport, MapId mapId, Box2 bounds, int groupIndex)
    {
        if (!_viewports.TryGetValue(viewport, out var sources))
        {
            sources = new ViewportSources();
            _viewports.Add(viewport, sources);
        }

        if (sources.Frame == _timing.CurFrame && sources.MapId == mapId && sources.Bounds.Equals(bounds))
            return sources.Groups[groupIndex];

        sources.Frame = _timing.CurFrame;
        sources.MapId = mapId;
        sources.Bounds = bounds;
        sources.All.Clear();
        foreach (var group in sources.Groups)
            group.Clear();

        _lookup.GetEntitiesIntersecting(mapId, bounds, sources.All);
        foreach (var entry in sources.All)
        {
            var index = GetGroupIndex(entry.Comp.DrawDepth);
            if (index >= 0)
                sources.Groups[index].Add(entry);
        }

        return sources.Groups[groupIndex];
    }

    private static int GetGroupIndex(int depth)
    {
        if (depth <= (int) Content.Shared.DrawDepth.DrawDepth.SmallMobs) return 0;
        if (depth >= (int) Content.Shared.DrawDepth.DrawDepth.Walls && depth <= (int) Content.Shared.DrawDepth.DrawDepth.WallTops) return 1;
        if (depth >= (int) Content.Shared.DrawDepth.DrawDepth.Objects && depth <= (int) Content.Shared.DrawDepth.DrawDepth.SmallObjects) return 2;
        if (depth == (int) Content.Shared.DrawDepth.DrawDepth.WallMountedItems) return 3;
        if (depth == (int) Content.Shared.DrawDepth.DrawDepth.LargeObjects) return 4;
        if (depth >= (int) Content.Shared.DrawDepth.DrawDepth.Items && depth <= (int) Content.Shared.DrawDepth.DrawDepth.BelowMobs) return 5;
        if (depth >= (int) Content.Shared.DrawDepth.DrawDepth.Mobs && depth <= (int) Content.Shared.DrawDepth.DrawDepth.OverMobs) return 6;
        if (depth >= (int) Content.Shared.DrawDepth.DrawDepth.Doors && depth <= (int) Content.Shared.DrawDepth.DrawDepth.Overdoors) return 7;
        if (depth > (int) Content.Shared.DrawDepth.DrawDepth.Overdoors && depth <= (int) Content.Shared.DrawDepth.DrawDepth.Overlays) return 8;
        return -1;
    }

    private sealed class ViewportSources
    {
        public uint Frame = uint.MaxValue;
        public MapId MapId = MapId.Nullspace;
        public Box2 Bounds;
        public readonly HashSet<Entity<SpriteComponent>> All = new();
        public readonly List<Entity<SpriteComponent>>[] Groups =
        [
            [], [], [], [], [], [], [], [], []
        ];
    }
}
