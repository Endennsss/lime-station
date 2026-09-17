using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Lime.Shaders.Bloom;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Builds and composites a quarter-resolution emissive bloom for one draw-depth interval.</summary>
internal abstract partial class LimeLampBloomOverlay : Overlay
{
    private const float ResolutionScale = 0.25f;
    private const float MinimumBlurRadius = 1.25f;
    private const float MaximumBlurRadius = 12f;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private static readonly ProtoId<ShaderPrototype> ExtractShader = "LimeBloomExtract";
    private static readonly ProtoId<ShaderPrototype> BlurShader = "LimeBloomBlur";
    private static readonly ProtoId<ShaderPrototype> CompositeShader = "LimeBloomComposite";

    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly EntityQuery<PointLightComponent> _lights;
    private readonly EntityQuery<LimeEmissiveBloomComponent> _emissiveQuery;
    private readonly EntityQuery<LimeLampBloomComponent> _lampQuery;
    private readonly LimeBloomSourceCache _sourceCache;
    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly ShaderInstance _extract;
    private readonly ShaderInstance _blurHorizontal;
    private readonly ShaderInstance _blurVertical;
    private readonly ShaderInstance _composite;
    private readonly int _minimumDepth;
    private readonly int _maximumDepth;
    private readonly int _groupIndex;

    public float Strength;

    // Каждый проход композитится после своей группы и перекрывается следующими объектами.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    protected LimeLampBloomOverlay(int groupIndex, int minimumDepth, int maximumDepth, LimeBloomSourceCache sourceCache)
    {
        _groupIndex = groupIndex;
        _sourceCache = sourceCache;
        _minimumDepth = minimumDepth;
        _maximumDepth = maximumDepth;
        IoCManager.InjectDependencies(this);
        _transform = _entity.System<TransformSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _lights = _entity.GetEntityQuery<PointLightComponent>();
        _emissiveQuery = _entity.GetEntityQuery<LimeEmissiveBloomComponent>();
        _lampQuery = _entity.GetEntityQuery<LimeLampBloomComponent>();
        _extract = _prototype.Index(ExtractShader).InstanceUnique();
        _blurHorizontal = _prototype.Index(BlurShader).InstanceUnique();
        _blurVertical = _prototype.Index(BlurShader).InstanceUnique();
        _composite = _prototype.Index(CompositeShader).InstanceUnique();
        ZIndex = maximumDepth + 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (Strength <= 0f || args.Viewport.Eye == null || args.MapId == MapId.Nullspace)
            return false;

        foreach (var entry in GetSources(args))
        {
            var sprite = entry.Comp;
            if (!IsRenderable(sprite))
                continue;

            if (_lampQuery.HasComp(entry) || _emissiveQuery.HasComp(entry))
                return true;

            foreach (var layer in sprite.AllLayers)
            {
                if (layer is SpriteComponent.Layer { Visible: true, Blank: false } spriteLayer &&
                    spriteLayer.ShaderPrototype == SpriteSystem.UnshadedId)
                    return true;
            }
        }

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null)
            return;

        var targetSize = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(args.Viewport.RenderTarget.Size.X * ResolutionScale)),
            Math.Max(1, (int) MathF.Ceiling(args.Viewport.RenderTarget.Size.Y * ResolutionScale)));
        var resources = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        EnsureTargets(resources, targetSize);

        var target = resources.Mask!;
        var ping = resources.Ping!;
        var pong = resources.Pong!;
        var targetScale = targetSize / (Vector2) args.Viewport.Size;
        var renderScale = args.Viewport.RenderScale * targetScale;
        var worldToTarget = target.GetWorldToLocalMatrix(eye, renderScale);
        var handle = args.WorldHandle;
        var sources = GetSources(args);
        var drewSource = false;
        var maximumRadius = 0f;

        try
        {
            handle.RenderInRenderTarget(target, () =>
            {
                foreach (var entry in sources)
                {
                    DrawLampMask(handle, entry, worldToTarget, ref drewSource, ref maximumRadius);
                    DrawEmissiveMasks(handle, entry, eye.Rotation, worldToTarget, ref drewSource, ref maximumRadius);
                }
            }, Color.Transparent);

            if (!drewSource)
                return;

            // Радиус переводится из мировых единиц в тексели quarter-resolution цели.
            // Так свечение сохраняет мировой размер при смене разрешения, zoom и render scale.
            var pixelsPerMeter = Vector2.TransformNormal(Vector2.UnitX, worldToTarget).Length();
            var blurRadius = Math.Clamp(maximumRadius * pixelsPerMeter, MinimumBlurRadius, MaximumBlurRadius);
            var texelSize = Vector2.One / (Vector2) targetSize;

            DrawBlurPass(handle, target.Texture, ping, _blurHorizontal, texelSize, Vector2.UnitX, blurRadius);
            DrawBlurPass(handle, ping.Texture, pong, _blurVertical, texelSize, Vector2.UnitY, blurRadius);

            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_composite);
            handle.DrawTextureRect(pong.Texture, args.WorldBounds, Color.White);
        }
        finally
        {
            handle.UseShader(null);
            handle.SetTransform(Matrix3x2.Identity);
        }
    }

    private void DrawLampMask(DrawingHandleWorld handle, Entity<SpriteComponent> entry, Matrix3x2 worldToTarget,
        ref bool drewSource, ref float maximumRadius)
    {
        if (!_lampQuery.TryComp(entry, out var bloom) || !bloom.Enabled ||
            !_lights.TryComp(entry, out var light) || !light.Enabled || light.Energy <= 0f ||
            !IsRenderable(entry.Comp))
            return;

        var texture = _sprite.Frame0(bloom.MaskSprite);
        var size = (Vector2) texture.Size / EyeManager.PixelsPerMeter *
                   Vector2.Clamp(bloom.MaskScale, new Vector2(0.01f), new Vector2(2f));
        var intensity = Strength * Math.Clamp(bloom.CoreStrength, 0f, 2f);
        if (!SetExtractParameters(handle, light.Color * bloom.BloomColor, intensity))
            return;

        handle.SetTransform(_transform.GetWorldMatrix(entry) * worldToTarget);
        handle.DrawTextureRect(texture, Box2.CenteredAround(bloom.MaskOffset, size), Color.White);
        maximumRadius = Math.Max(maximumRadius, bloom.HaloRadius);
        drewSource = true;
    }

    private void DrawEmissiveMasks(DrawingHandleWorld handle, Entity<SpriteComponent> entry, Angle eyeRotation,
        Matrix3x2 worldToTarget, ref bool drewSource, ref float maximumRadius)
    {
        var sprite = entry.Comp;
        if (!IsRenderable(sprite) || _lampQuery.HasComp(entry))
            return;

        _emissiveQuery.TryComp(entry, out var settings);
        if (settings != null)
        {
            foreach (var key in settings.Layers)
            {
                if (_sprite.TryGetLayer(entry.AsNullable(), key, out var layer, false))
                    DrawItemLayer(handle, entry, layer, eyeRotation, worldToTarget, settings,
                        ref drewSource, ref maximumRadius);
            }
            return;
        }

        foreach (var spriteLayer in sprite.AllLayers)
        {
            if (spriteLayer is SpriteComponent.Layer layer && layer.ShaderPrototype == SpriteSystem.UnshadedId)
                DrawItemLayer(handle, entry, layer, eyeRotation, worldToTarget, null,
                    ref drewSource, ref maximumRadius);
        }
    }

    private void DrawItemLayer(DrawingHandleWorld handle, Entity<SpriteComponent> entry, SpriteComponent.Layer layer,
        Angle eyeRotation, Matrix3x2 worldToTarget, LimeEmissiveBloomComponent? settings,
        ref bool drewSource, ref float maximumRadius)
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

        var renderRotation = sprite.NoRotation
            ? -eyeRotation
            : rotation - (sprite.SnapCardinals ? angle.RoundToCardinalAngle() : Angle.Zero);
        if (sprite.GranularLayersRendering)
        {
            renderRotation = layer.RenderingStrategy switch
            {
                LayerRenderingStrategy.Default => rotation,
                LayerRenderingStrategy.NoRotation => -eyeRotation,
                LayerRenderingStrategy.SnapToCardinals => rotation - angle.RoundToCardinalAngle(),
                _ => renderRotation
            };
        }

        var matrix = layerMatrix * sprite.LocalMatrix *
                     Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(entry), renderRotation) * worldToTarget;
        var color = sprite.Color * layer.Color * (settings?.Color ?? Color.White);
        if (!SetExtractParameters(handle, color, Strength * Math.Clamp(strength, 0f, 2f)))
            return;

        handle.SetTransform(matrix);
        handle.DrawTextureRect(texture,
            Box2.CenteredAround(Vector2.Zero, (Vector2) texture.Size / EyeManager.PixelsPerMeter), Color.White);
        maximumRadius = Math.Max(maximumRadius, radius);
        drewSource = true;
    }

    private bool SetExtractParameters(DrawingHandleWorld handle, Color color, float intensity)
    {
        if (!float.IsFinite(intensity) || intensity <= 0f)
            return false;

        _extract.SetParameter("bloom_color", color);
        _extract.SetParameter("bloom_strength", intensity);
        handle.UseShader(_extract);
        return true;
    }

    private static void DrawBlurPass(DrawingHandleWorld handle, Texture source, IRenderTarget destination,
        ShaderInstance shader, Vector2 texelSize, Vector2 direction, float radius)
    {
        shader.SetParameter("texel_size", texelSize);
        shader.SetParameter("blur_direction", direction);
        shader.SetParameter("blur_radius", radius);

        handle.RenderInRenderTarget(destination, () =>
        {
            try
            {
                handle.SetTransform(Matrix3x2.Identity);
                handle.UseShader(shader);
                handle.DrawTextureRect(source, Box2.FromDimensions(Vector2.Zero, destination.Size), Color.White);
            }
            finally
            {
                handle.UseShader(null);
                handle.SetTransform(Matrix3x2.Identity);
            }
        }, Color.Transparent);
    }

    private bool IsRenderable(SpriteComponent sprite)
    {
        return MatchesDepth(sprite) && sprite.Visible && !sprite.ContainerOccluded && !HasBlockingEffect(sprite);
    }

    private bool HasBlockingEffect(SpriteComponent sprite)
    {
        // Выделение при наведении не отключает свечение; остальные post-shader эффекты исключают источник.
        foreach (var effect in _sprite.GetPostShaders(sprite))
        {
            if (effect.Id != ContentPostShaderIds.InteractionOutline &&
                effect.Id != ContentPostShaderIds.TargetOutline &&
                effect.Id != ContentPostShaderIds.DragDropOutline)
                return true;
        }
        return false;
    }

    private List<Entity<SpriteComponent>> GetSources(in OverlayDrawArgs args)
    {
        return _sourceCache.Get(args.Viewport, args.MapId, args.WorldAABB.Enlarged(3f), _groupIndex);
    }

    private bool MatchesDepth(SpriteComponent sprite)
    {
        return sprite.DrawDepth >= _minimumDepth && sprite.DrawDepth <= _maximumDepth;
    }

    private void EnsureTargets(CachedResources resources, Vector2i size)
    {
        if (resources.Mask?.Texture.Size == size &&
            resources.Ping?.Texture.Size == size &&
            resources.Pong?.Texture.Size == size)
            return;

        resources.Mask?.Dispose();
        resources.Ping?.Dispose();
        resources.Pong?.Dispose();
        var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
        var samples = new TextureSampleParameters { Filter = true };
        resources.Mask = _clyde.CreateRenderTarget(size, format, samples, "lime-bloom-mask");
        resources.Ping = _clyde.CreateRenderTarget(size, format, samples, "lime-bloom-ping");
        resources.Pong = _clyde.CreateRenderTarget(size, format, samples, "lime-bloom-pong");
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _extract.Dispose();
        _blurHorizontal.Dispose();
        _blurVertical.Dispose();
        _composite.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Mask;
        public IRenderTexture? Ping;
        public IRenderTexture? Pong;

        public void Dispose()
        {
            Mask?.Dispose();
            Ping?.Dispose();
            Pong?.Dispose();
        }
    }
}

/// <summary>Собирает видимые источники один раз за кадр для всех проходов глубины.</summary>
internal sealed class LimeBloomSourceCache
{
    private readonly EntityLookupSystem _lookup;
    private readonly IGameTiming _timing;
    private readonly Dictionary<long, ViewportSources> _viewports = new();

    public LimeBloomSourceCache(IEntityManager entity)
    {
        _lookup = entity.System<EntityLookupSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    public List<Entity<SpriteComponent>> Get(IClydeViewport viewport, MapId mapId, Box2 bounds, int groupIndex)
    {
        if (!_viewports.TryGetValue(viewport.Id, out var sources))
        {
            sources = new ViewportSources();
            _viewports.Add(viewport.Id, sources);
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
        if (depth <= (int) Content.Shared.DrawDepth.DrawDepth.SmallMobs)
            return 0;
        if (depth <= (int) Content.Shared.DrawDepth.DrawDepth.LargeObjects)
            return 1;
        if (depth <= (int) Content.Shared.DrawDepth.DrawDepth.OverMobs)
            return 2;
        if (depth <= (int) Content.Shared.DrawDepth.DrawDepth.Overlays)
            return 3;
        return -1;
    }

    private sealed class ViewportSources
    {
        public uint Frame = uint.MaxValue;
        public MapId MapId = MapId.Nullspace;
        public Box2 Bounds;
        public readonly HashSet<Entity<SpriteComponent>> All = new();
        public readonly List<Entity<SpriteComponent>>[] Groups = [[], [], [], []];
    }
}
