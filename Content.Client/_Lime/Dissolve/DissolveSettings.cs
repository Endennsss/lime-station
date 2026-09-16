namespace Content.Client._Lime.Dissolve;

/// <summary>Dissolve direction in the sprite's screen plane.</summary>
public enum DissolveDirection : byte
{
    Noise,
    BottomToTop,
    TopToBottom,
}

/// <summary>Local visual settings that do not modify gameplay state.</summary>
public sealed record DissolveSettings
{
    /// <summary>Bright edge width in the zero-to-one noise range.</summary>
    public float EdgeWidth { get; init; } = 0.06f;
    /// <summary>Outer hot edge color.</summary>
    public Color EdgeColor { get; init; } = Color.FromHex("#FF5008");
    /// <summary>Narrow inner edge color.</summary>
    public Color CoreColor { get; init; } = Color.FromHex("#FFF1A0");
    /// <summary>Glow intensity from zero to four.</summary>
    public float EdgeIntensity { get; init; } = 1.4f;
    /// <summary>Number of coarse noise cells per sprite meter.</summary>
    public float NoiseScale { get; init; } = 5f;
    /// <summary>Charred region width ahead of the hot edge.</summary>
    public float CharWidth { get; init; } = 0.08f;
    /// <summary>Darkening fraction of the charred region.</summary>
    public float CharStrength { get; init; } = 0.8f;
    /// <summary>Fixed noise offset preserving the pattern when reversing the animation.</summary>
    public float Seed { get; init; } = 1f;
    /// <summary>Dissolve ordering.</summary>
    public DissolveDirection Direction { get; init; }

    public static readonly DissolveSettings Burn = new();
    public static readonly DissolveSettings Teleport = new()
    {
        EdgeColor = Color.FromHex("#009DFF"), CoreColor = Color.FromHex("#A0FFFF"),
        CharStrength = 0f, Direction = DissolveDirection.BottomToTop,
    };
    public static readonly DissolveSettings Anomaly = new()
    {
        EdgeColor = Color.FromHex("#A000FF"), CoreColor = Color.FromHex("#FF80FF"),
        NoiseScale = 8f, EdgeWidth = 0.09f, CharStrength = 0.35f,
    };
    public static readonly DissolveSettings Energy = new()
    {
        EdgeColor = Color.FromHex("#20DFFF"), CoreColor = Color.White,
        EdgeWidth = 0.025f, EdgeIntensity = 2f, CharStrength = 0f,
    };
}
