namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>Distinct overlay type for each bloom depth interval.</summary>
internal sealed class LimeDepthBloomOverlay<T> : LimeLampBloomOverlay
{
    public LimeDepthBloomOverlay(int groupIndex, int minimumDepth, int maximumDepth, LimeBloomSourceCache sourceCache)
        : base(groupIndex, minimumDepth, maximumDepth, sourceCache)
    {
    }
}
