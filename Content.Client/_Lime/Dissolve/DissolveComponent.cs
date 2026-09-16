using Robust.Client.Graphics;

namespace Content.Client._Lime.Dissolve;

/// <summary>Temporary client dissolve state and saved sprite visibility.</summary>
[RegisterComponent]
public sealed partial class DissolveComponent : Component
{
    /// <summary>Owned mutable post-processing shader.</summary>
    public ShaderInstance? Shader;
    /// <summary>Sprite visibility saved before starting the effect.</summary>
    public bool PreviousVisible;
    /// <summary>Original shader event subscription flag.</summary>
    public bool PreviousRaiseShaderEvent;
    /// <summary>Original screen texture request flag.</summary>
    public bool PreviousGetScreenTexture;
    /// <summary>Whether this effect hid the sprite on completion.</summary>
    public bool Hidden;
    /// <summary>Current normalized dissolve phase.</summary>
    public float Amount;
    /// <summary>Animation's initial normalized phase.</summary>
    public float From;
    /// <summary>Animation's target normalized phase.</summary>
    public float To;
    /// <summary>Elapsed animation time in seconds.</summary>
    public float Elapsed;
    /// <summary>Animation duration in seconds.</summary>
    public float Duration;
}
