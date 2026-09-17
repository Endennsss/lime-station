using System.Numerics;
using Content.Shared._Lime.Movement;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Depth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Lime.Movement;

public sealed partial class LimeMobilityVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimeActiveManeuverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LimeActiveManeuverComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LimeProneComponent, ComponentStartup>(OnProneStartup);
        SubscribeLocalEvent<LimeProneComponent, ComponentRemove>(OnProneRemove);
        SubscribeLocalEvent<SpriteComponent, ComponentStartup>(OnSpriteStartup);
    }

    private void OnStartup(Entity<LimeActiveManeuverComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        var visuals = EnsureComp<LimeMobilityVisualsComponent>(entity);
        RemoveJumpOffset((entity.Owner, visuals), sprite);
    }

    private void OnShutdown(Entity<LimeActiveManeuverComponent> entity, ref ComponentShutdown args)
    {
        if (!TryComp<LimeMobilityVisualsComponent>(entity, out var visuals) ||
            !TryComp<SpriteComponent>(entity, out var sprite))
            return;

        RemoveJumpOffset((entity.Owner, visuals), sprite);
        RemComp<LimeMobilityVisualsComponent>(entity.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var query = EntityQueryEnumerator<LimeActiveManeuverComponent, LimeMobilityComponent,
            LimeMobilityVisualsComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var maneuver, out var mobility, out var visuals, out var sprite))
        {
            var duration = maneuver.Type == LimeManeuverType.Roll ? mobility.RollDuration : mobility.JumpDuration;
            var remaining = (float) (maneuver.EndTime - _timing.CurTime).TotalSeconds;
            var progress = Math.Clamp(1f - remaining / duration, 0f, 1f);

            if (maneuver.Type == LimeManeuverType.Jump)
            {
                var height = MathF.Sin(progress * MathF.PI) * 0.3f;
                var offset = sprite.Offset - visuals.AppliedOffset + new Vector2(0f, height);
                visuals.AppliedOffset = new Vector2(0f, height);
                _sprites.SetOffset((uid, sprite), offset);
            }
            else
            {
                RemoveJumpOffset((uid, visuals), sprite);
            }
        }
    }

    private void RemoveJumpOffset(Entity<LimeMobilityVisualsComponent> entity, SpriteComponent sprite)
    {
        if (entity.Comp.AppliedOffset == Vector2.Zero)
            return;

        _sprites.SetOffset((entity.Owner, sprite), sprite.Offset - entity.Comp.AppliedOffset);
        entity.Comp.AppliedOffset = Vector2.Zero;
    }

    private void OnSpriteStartup(Entity<SpriteComponent> entity, ref ComponentStartup args)
    {
        if (TryComp<LimeMobilityVisualsComponent>(entity, out var visuals))
            visuals.AppliedOffset = Vector2.Zero;

        if (TryComp<LimeProneComponent>(entity, out _))
            OnProneSpriteStartup(entity);
    }

    private void OnProneSpriteStartup(Entity<SpriteComponent> entity)
    {
        var visuals = EnsureComp<LimeProneVisualsComponent>(entity);
        visuals.BaseDrawDepth = entity.Comp.DrawDepth;
        _sprites.SetDrawDepth(entity.AsNullable(), (int) Depth.SmallMobs);
    }

    private void OnProneStartup(Entity<LimeProneComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;
        OnProneSpriteStartup((entity.Owner, sprite));
    }

    private void OnProneRemove(Entity<LimeProneComponent> entity, ref ComponentRemove args)
    {
        if (!TryComp<LimeProneVisualsComponent>(entity, out var visuals) ||
            !TryComp<SpriteComponent>(entity, out var sprite))
            return;
        _sprites.SetDrawDepth((entity.Owner, sprite), visuals.BaseDrawDepth);
        RemComp<LimeProneVisualsComponent>(entity);
    }
}

[RegisterComponent]
public sealed partial class LimeMobilityVisualsComponent : Component
{
    public Vector2 AppliedOffset;
}

[RegisterComponent]
public sealed partial class LimeProneVisualsComponent : Component
{
    public int BaseDrawDepth;
}
