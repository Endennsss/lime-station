using System.Numerics;
using Content.Client.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Builds three viewport-sized emissive masks without blurring the scene itself.</summary>
internal sealed class LimeEmissiveBloomPass : IDisposable
{
    private const int PaddingPixels = 240;
    private static readonly int[] Downsamples = [2, 4, 8];
    private static readonly float[] BlurMultipliers = [3f, 10f, 26f];
    private static readonly ProtoId<ShaderPrototype> CompositeShader = "LimeEmissiveBloom";
    private static readonly ProtoId<ShaderPrototype> MaskShader = "LimeEmissiveMask";
    private readonly IClyde _clyde;
    private readonly ShaderInstance _composite;
    private readonly ShaderInstance _mask;
    private readonly List<Source> _sources = new();
    private readonly OverlayResourceCache<Targets> _resources = new();

    public LimeEmissiveBloomPass(IClyde clyde, IPrototypeManager prototype)
    {
        _clyde = clyde;
        _composite = prototype.Index(CompositeShader).InstanceUnique();
        _mask = prototype.Index(MaskShader).Instance();
    }

    public void Clear() => _sources.Clear();

    public void Add(Texture texture, Matrix3x2 matrix, Color color, float strength, float radius)
    {
        _sources.Add(new Source(texture, matrix, color, strength, radius / 2f));
    }

    public float GetPadding(IClydeViewport viewport)
    {
        return PaddingPixels * viewport.Eye!.Zoom.X / (EyeManager.PixelsPerMeter * viewport.RenderScale.X);
    }

    public void Draw(in OverlayDrawArgs args, Texture scene, float strength)
    {
        if (_sources.Count == 0)
            return;
        var viewport = args.Viewport;
        var eye = viewport.Eye!;
        var handle = args.WorldHandle;
        var targets = _resources.GetForViewport(viewport, static _ => new Targets());
        var paddedSize = viewport.Size + new Vector2i(PaddingPixels * 2, PaddingPixels * 2);
        try
        {
            for (var level = 0; level < Downsamples.Length; level++)
            {
                var size = new Vector2i((paddedSize.X + Downsamples[level] - 1) / Downsamples[level],
                    (paddedSize.Y + Downsamples[level] - 1) / Downsamples[level]);
                if (targets.Masks[level] == null || targets.Masks[level]!.Size != size)
                {
                    targets.Masks[level]?.Dispose();
                    targets.Buffers[level]?.Dispose();
                    targets.Masks[level] = CreateTarget(size);
                    targets.Buffers[level] = CreateTarget(size);
                }

                var mask = targets.Masks[level]!;
                var worldToMask = mask.GetWorldToLocalMatrix(eye, viewport.RenderScale / Downsamples[level]);
                var currentLevel = level;
                handle.RenderInRenderTarget(mask, () =>
                {
                    handle.UseShader(_mask);
                    foreach (var source in _sources)
                    {
                        var weight = currentLevel switch
                        {
                            0 => 0.85f - source.Radius * 0.25f,
                            1 => 0.35f + source.Radius * 0.55f,
                            _ => 0.1f + source.Radius * 0.55f
                        };
                        // Усиливаем энергию RGB, а не прозрачность всего спрайта.
                        var energy = source.Strength * weight;
                        var tint = new Color(source.Color.R * energy, source.Color.G * energy,
                            source.Color.B * energy, source.Color.A);
                        handle.SetTransform(source.Matrix * worldToMask);
                        handle.DrawTextureRect(source.Texture,
                            Box2.CenteredAround(Vector2.Zero, (Vector2) source.Texture.Size / EyeManager.PixelsPerMeter), tint);
                    }
                }, Color.Black.WithAlpha(0f));
                _clyde.BlurRenderTarget(viewport, mask, targets.Buffers[level]!, eye, BlurMultipliers[level]);
            }

            _composite.SetParameter("nearGlow", targets.Masks[0]!.Texture);
            _composite.SetParameter("middleGlow", targets.Masks[1]!.Texture);
            _composite.SetParameter("wideGlow", targets.Masks[2]!.Texture);
            _composite.SetParameter("sceneTexture", scene);
            _composite.SetParameter("strength", strength);
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_composite);
            // Чёткое ядро уже нарисовано нативным слоем: повторно поверх предметов его не рисуем.
            handle.DrawTextureRect(targets.Masks[0]!.Texture, args.WorldBounds.Enlarged(GetPadding(viewport)), Color.White);
        }
        finally
        {
            handle.UseShader(null);
            handle.SetTransform(Matrix3x2.Identity);
        }
    }

    private IRenderTexture CreateTarget(Vector2i size)
    {
        return _clyde.CreateRenderTarget(size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            new TextureSampleParameters { Filter = true });
    }

    public void Dispose()
    {
        _resources.Dispose();
        _composite.Dispose();
        _sources.Clear();
    }

    private readonly record struct Source(Texture Texture, Matrix3x2 Matrix, Color Color, float Strength, float Radius);

    private sealed class Targets : IDisposable
    {
        public readonly IRenderTexture?[] Masks = new IRenderTexture?[3];
        public readonly IRenderTexture?[] Buffers = new IRenderTexture?[3];

        public void Dispose()
        {
            for (var i = 0; i < Masks.Length; i++)
            {
                Masks[i]?.Dispose();
                Buffers[i]?.Dispose();
            }
        }
    }
}
