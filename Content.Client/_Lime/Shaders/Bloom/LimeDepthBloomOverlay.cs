namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Distinct overlay type for each native sprite-depth interval.</summary>
internal sealed class LimeDepthBloomOverlay<T> : LimeLampBloomOverlay
{
    public LimeDepthBloomOverlay(int minimumDepth, int maximumDepth, LimeBloomSourceCache sourceCache)
        : base(minimumDepth, maximumDepth, sourceCache)
    {
    }
}
