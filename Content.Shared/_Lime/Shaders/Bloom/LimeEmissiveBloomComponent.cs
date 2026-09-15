using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Lime.Shaders.Bloom;

/// <summary>
/// Adds source-local decorative halos to selected sprite layers without creating a point light.
/// </summary>
[RegisterComponent]
public sealed partial class LimeEmissiveBloomComponent : Component
{
    /// <summary>
    /// Sprite layer keys used to position decorative halos.
    /// </summary>
    [DataField(required: true)]
    public List<string> Layers = [];

    /// <summary>
    /// Local intensity multiplier applied on top of the user's bloom setting.
    /// </summary>
    [DataField]
    public float Strength = 0.8f;

    /// <summary>
    /// Local halo width profile; zero disables the halo.
    /// </summary>
    [DataField]
    public float Radius = 1.2f;

    /// <summary>
    /// Additional color modulation preserving the texture's original color.
    /// </summary>
    [DataField]
    public Color Color = Color.White;
}
