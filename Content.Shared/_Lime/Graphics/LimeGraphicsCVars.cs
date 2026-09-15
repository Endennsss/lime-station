using Robust.Shared.Configuration;

namespace Content.Shared._Lime.Graphics;

/// <summary>Client preferences for Lime Station bloom rendering.</summary>
[CVarDefs]
public static class LimeGraphicsCVars
{
    /// <summary>Enables bloom rendering; disabling it avoids the additional render passes.</summary>
    public static readonly CVarDef<bool> LightBloomEnabled =
        CVarDef.Create("lime.light_bloom_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Global bloom intensity from zero to one.</summary>
    public static readonly CVarDef<float> LightBloomStrength =
        CVarDef.Create("lime.light_bloom_strength", 0.45f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
