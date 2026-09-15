using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Кэш плавно размытого свечения неизменяемых кадров RSI.</summary>
internal sealed class LimeItemBloomCache : IDisposable
{
    private const int Padding = 12;
    private const int MaxEntries = 256;
    private const int MaxBytes = 16 * 1024 * 1024;
    private static readonly ProtoId<ShaderPrototype> HorizontalShader = "LimeItemBloom";
    private static readonly ProtoId<ShaderPrototype> VerticalShader = "LimeItemBlur";
    private readonly IClyde _clyde;
    private readonly ShaderInstance _horizontal;
    private readonly ShaderInstance _vertical;
    private readonly Dictionary<(Texture Texture, int Radius), IRenderTexture> _cache = new();
    private readonly Queue<(Texture Texture, int Radius)> _order = new();
    private IRenderTexture? _scratch;
    private int _bytes;

    public LimeItemBloomCache(IClyde clyde, IPrototypeManager prototype)
    {
        _clyde = clyde;
        _horizontal = prototype.Index(HorizontalShader).InstanceUnique();
        _vertical = prototype.Index(VerticalShader).InstanceUnique();
    }

    public IRenderTexture? Get(DrawingHandleWorld handle, Texture texture, float radius)
    {
        if (texture.Size.X <= 0 || texture.Size.Y <= 0 || texture.Size.X > 512 || texture.Size.Y > 512)
            return null;
        var blur = (int)MathF.Round(Math.Clamp(radius * 3.5f, 2f, 8f));
        var key = (texture, blur);
        var found = _cache.TryGetValue(key, out var cached);
        // Кадр RSI неизменяемый; отдельные PNG и динамические текстуры обновляем в существующем RT.
        if (found && texture is AtlasTexture)
            return cached;

        var size = texture.Size + new Vector2i(Padding * 2, Padding * 2);
        var bytes = size.X * size.Y * 4;
        while (!found && (_cache.Count >= MaxEntries || _bytes + bytes > MaxBytes))
        {
            var oldest = _order.Dequeue();
            var expired = _cache[oldest];
            _bytes -= expired.Size.X * expired.Size.Y * 4;
            expired.Dispose();
            _cache.Remove(oldest);
        }
        if (_scratch == null || _scratch.Size != size)
        {
            _scratch?.Dispose();
            _scratch = CreateTarget(size);
        }
        var target = cached ?? CreateTarget(size);
        var pixels = (Vector2)texture.Size;
        var atlasPixels = texture is AtlasTexture atlas ? (Vector2)atlas.SourceTexture.Size : pixels;
        _horizontal.SetParameter("uvScale", pixels / atlasPixels);
        _horizontal.SetParameter("sourcePixels", pixels);
        _horizontal.SetParameter("paddingPixels", (float)Padding);
        _horizontal.SetParameter("blurPixels", (float)blur);
        _vertical.SetParameter("blurPixels", (float)blur);
        var bounds = new Box2(Vector2.Zero, (Vector2)size);
        handle.RenderInRenderTarget(_scratch, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_horizontal);
            handle.DrawTextureRect(texture, bounds);
        }, Color.Transparent);
        handle.RenderInRenderTarget(target, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_vertical);
            handle.DrawTextureRect(_scratch.Texture, bounds);
        }, Color.Transparent);
        if (!found)
        {
            _cache.Add(key, target);
            _order.Enqueue(key);
            _bytes += bytes;
        }
        return target;
    }

    private IRenderTexture CreateTarget(Vector2i size)
    {
        return _clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            new TextureSampleParameters { Filter = true });
    }

    public void Dispose()
    {
        foreach (var target in _cache.Values)
            target.Dispose();
        _cache.Clear();
        _order.Clear();
        _bytes = 0;
        _scratch?.Dispose();
        _scratch = null;
        _horizontal.Dispose();
        _vertical.Dispose();
    }
}
