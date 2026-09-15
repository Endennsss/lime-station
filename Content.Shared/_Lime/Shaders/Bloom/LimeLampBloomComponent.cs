using System.Numerics;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared._Lime.Shaders.Bloom;

/// <summary>Local decorative light shape, independent of illumination range.</summary>
[RegisterComponent]
public sealed partial class LimeLampBloomComponent : Component
{
    /// <summary>Whether decorative glow is enabled.</summary>
    [DataField] public bool Enabled = true;
    /// <summary>Licensed texture defining the luminous core.</summary>
    [DataField] public SpriteSpecifier MaskSprite = new SpriteSpecifier.Rsi(new ResPath("_Lime/Effects/LightMasks/64.rsi"), "light_point");
    /// <summary>Core center in entity-local meters.</summary>
    [DataField] public Vector2 MaskOffset = new(0f, 0.45f);
    /// <summary>Texture scale independent of light radius.</summary>
    [DataField] public Vector2 MaskScale = Vector2.One;
    /// <summary>Additional modulation of the current light color.</summary>
    [DataField] public Color BloomColor = Color.White;
    /// <summary>Core intensity multiplier.</summary>
    [DataField] public float CoreStrength = 0.7f;
    /// <summary>Local halo width profile, not the illumination radius.</summary>
    [DataField] public float HaloRadius = 1.2f;
    /// <summary>Halo falloff softness from zero to one.</summary>
    [DataField] public float Softness = 0.75f;
}
