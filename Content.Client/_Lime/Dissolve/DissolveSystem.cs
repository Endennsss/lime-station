using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lime.Dissolve;

/// <summary>Dissolves the assembled client sprite through a unique post-processing shader.</summary>
public sealed class DissolveSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "LimeDissolve";
    private readonly List<EntityUid> _finished = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DissolveComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DissolveComponent, BeforePostShaderRenderEvent>(OnRender);
        SubscribeLocalEvent<SpriteComponent, ComponentShutdown>(OnSpriteShutdown);
    }

    public override void Shutdown()
    {
        var query = AllEntityQuery<DissolveComponent>();
        while (query.MoveNext(out var uid, out var dissolve))
            Restore(uid, dissolve);
        _finished.Clear();
        base.Shutdown();
    }

    private void OnShutdown(Entity<DissolveComponent> ent, ref ComponentShutdown args)
    {
        Restore(ent, ent.Comp);
        RemComp<ActiveDissolveComponent>(ent);
    }

    private void OnSpriteShutdown(Entity<SpriteComponent> ent, ref ComponentShutdown args)
    {
        Reset(ent);
    }

    private void OnRender(Entity<DissolveComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (ent.Comp.Shader is not { } shader || args.Sprite.PostShader != shader || args.Viewport.Eye is not { } eye)
            return;

        // RT переиспользуется для спрайтов разных размеров. UV нельзя считать UV самого объекта.
        var pixelsPerMeter = args.Viewport.RenderScale * EyeManager.PixelsPerMeter / eye.Zoom;
        shader.SetParameter("pixels_per_meter", pixelsPerMeter);
        var bounds = _sprite.GetLocalBounds((ent.Owner, args.Sprite));
        var rotation = args.Sprite.NoRotation
            ? args.Sprite.Rotation
            : args.Sprite.Rotation + _transform.GetWorldRotation(ent.Owner) + eye.Rotation;
        var height = MathF.Abs((float) Math.Sin(rotation.Theta)) * bounds.Width +
            MathF.Abs((float) Math.Cos(rotation.Theta)) * bounds.Height;
        shader.SetParameter("sprite_height", MathF.Max(height, 0.01f));
    }

    /// <summary>Animates from zero to one, hiding only the client sprite on completion.</summary>
    public bool StartDissolve(EntityUid uid, float duration, DissolveSettings? settings = null)
    {
        return Start(uid, duration, 0f, 1f, settings ?? DissolveSettings.Burn);
    }

    /// <summary>Materializes from one to zero, restoring the original sprite state.</summary>
    public bool StartMaterialize(EntityUid uid, float duration, DissolveSettings? settings = null)
    {
        return Start(uid, duration, 1f, 0f, settings ?? DissolveSettings.Burn);
    }

    /// <summary>Sets a static preview phase. Reset removes the effect.</summary>
    public bool SetAmount(EntityUid uid, float amount, DissolveSettings? settings = null)
    {
        if (!float.IsFinite(amount))
            return false;
        return Start(uid, 0f, Math.Clamp(amount, 0f, 1f), Math.Clamp(amount, 0f, 1f), settings ?? DissolveSettings.Burn);
    }

    /// <summary>Cancels the animation and restores the original visibility.</summary>
    public void Reset(EntityUid uid)
    {
        RemComp<ActiveDissolveComponent>(uid);
        RemComp<DissolveComponent>(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _finished.Clear();
        var query = EntityQueryEnumerator<ActiveDissolveComponent, DissolveComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var dissolve, out var sprite))
        {
            if (sprite.PostShader != dissolve.Shader)
            {
                _finished.Add(uid);
                continue;
            }

            dissolve.Elapsed += frameTime;
            var progress = Math.Clamp(dissolve.Elapsed / dissolve.Duration, 0f, 1f);
            dissolve.Amount = dissolve.From + (dissolve.To - dissolve.From) * progress;
            dissolve.Shader?.SetParameter("dissolve_amount", dissolve.Amount);
            if (progress >= 1f)
                _finished.Add(uid);
        }

        foreach (var uid in _finished)
        {
            if (!TryComp<DissolveComponent>(uid, out var dissolve))
                continue;
            RemComp<ActiveDissolveComponent>(uid);
            if (dissolve.Amount >= 1f && TryComp<SpriteComponent>(uid, out var sprite) && sprite.PostShader == dissolve.Shader)
                Hide(uid, dissolve, sprite);
            else
                RemComp<DissolveComponent>(uid);
        }
    }

    private bool Start(EntityUid uid, float duration, float from, float to, DissolveSettings settings)
    {
        if (!_timing.IsFirstTimePredicted || Terminating(uid) || !float.IsFinite(duration) || duration < 0f ||
            !Valid(settings) || !TryComp<SpriteComponent>(uid, out var sprite))
            return false;

        TryComp<DissolveComponent>(uid, out var existing);
        if (sprite.PostShader != null && sprite.PostShader != existing?.Shader)
            return false;

        if (existing != null)
        {
            from = existing.Amount;
            Reset(uid);
        }

        var dissolve = AddComp<DissolveComponent>(uid);
        dissolve.PreviousVisible = sprite.Visible;
        dissolve.PreviousRaiseShaderEvent = sprite.RaiseShaderEvent;
        dissolve.PreviousGetScreenTexture = sprite.GetScreenTexture;
        dissolve.From = from;
        dissolve.To = to;
        dissolve.Duration = duration;
        dissolve.Amount = duration > 0f ? from : to;
        var shader = _prototype.Index(ShaderId).InstanceUnique();
        dissolve.Shader = shader;
        shader.SetParameter("dissolve_amount", dissolve.Amount);
        shader.SetParameter("edge_width", settings.EdgeWidth);
        shader.SetParameter("edge_color", settings.EdgeColor);
        shader.SetParameter("core_color", settings.CoreColor);
        shader.SetParameter("edge_intensity", settings.EdgeIntensity);
        shader.SetParameter("noise_scale", settings.NoiseScale);
        shader.SetParameter("char_width", settings.CharWidth);
        shader.SetParameter("char_strength", settings.CharStrength);
        shader.SetParameter("noise_seed", settings.Seed);
        shader.SetParameter("direction", (float) settings.Direction);
        shader.SetParameter("pixels_per_meter", new Vector2(EyeManager.PixelsPerMeter));
        shader.SetParameter("sprite_height", 1f);
        sprite.PostShader = shader;
        sprite.RaiseShaderEvent = true;
        sprite.GetScreenTexture = false;

        if (duration > 0f)
            EnsureComp<ActiveDissolveComponent>(uid);
        else if (to >= 1f)
            Hide(uid, dissolve, sprite);
        else if (to <= 0f)
            Reset(uid);
        return true;
    }

    private void Hide(EntityUid uid, DissolveComponent dissolve, SpriteComponent sprite)
    {
        ReleaseShader(dissolve, sprite);
        dissolve.Hidden = true;
        _sprite.SetVisible((uid, sprite), false);
    }

    private void Restore(EntityUid uid, DissolveComponent dissolve)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            ReleaseShader(dissolve, sprite);
            if (dissolve.Hidden && !Terminating(uid))
                _sprite.SetVisible((uid, sprite), dissolve.PreviousVisible);
        }
        dissolve.Shader?.Dispose();
        dissolve.Shader = null;
    }

    private static void ReleaseShader(DissolveComponent dissolve, SpriteComponent sprite)
    {
        if (dissolve.Shader != null && sprite.PostShader == dissolve.Shader)
        {
            sprite.PostShader = null;
            sprite.RaiseShaderEvent = dissolve.PreviousRaiseShaderEvent;
            sprite.GetScreenTexture = dissolve.PreviousGetScreenTexture;
        }
        dissolve.Shader?.Dispose();
        dissolve.Shader = null;
    }

    private static bool Valid(DissolveSettings settings)
    {
        return float.IsFinite(settings.EdgeWidth) && settings.EdgeWidth is >= 0f and <= 0.25f &&
            float.IsFinite(settings.EdgeIntensity) && settings.EdgeIntensity is >= 0f and <= 4f &&
            float.IsFinite(settings.NoiseScale) && settings.NoiseScale is >= 0.1f and <= 32f &&
            float.IsFinite(settings.CharWidth) && settings.CharWidth is >= 0f and <= 0.25f &&
            float.IsFinite(settings.CharStrength) && settings.CharStrength is >= 0f and <= 1f &&
            float.IsFinite(settings.Seed) && MathF.Abs(settings.Seed) <= 1000f &&
            Enum.IsDefined(settings.Direction);
    }
}
