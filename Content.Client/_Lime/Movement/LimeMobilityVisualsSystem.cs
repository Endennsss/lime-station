using System.Numerics;
using Content.Shared._Lime.Movement;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Lime.Movement;

public sealed partial class LimeMobilityVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimeActiveManeuverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LimeActiveManeuverComponent, ComponentRemove>(OnRemove);
    }

    private void OnStartup(Entity<LimeActiveManeuverComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        var visuals = EnsureComp<LimeMobilityVisualsComponent>(entity);
        visuals.BaseOffset = sprite.Offset;
        visuals.BaseRotation = sprite.Rotation;
    }

    private void OnRemove(Entity<LimeActiveManeuverComponent> entity, ref ComponentRemove args)
    {
        if (!TryComp<LimeMobilityVisualsComponent>(entity, out var visuals) ||
            !TryComp<SpriteComponent>(entity, out var sprite))
            return;

        _sprites.SetOffset((entity.Owner, sprite), visuals.BaseOffset);
        _sprites.SetRotation((entity.Owner, sprite), visuals.BaseRotation);
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

            if (maneuver.Type == LimeManeuverType.Roll)
            {
                _sprites.SetRotation((uid, sprite), visuals.BaseRotation + Angle.FromDegrees(progress * 360f));
            }
            else
            {
                var height = MathF.Sin(progress * MathF.PI) * 0.3f;
                _sprites.SetOffset((uid, sprite), visuals.BaseOffset + new Vector2(0f, height));
            }
        }
    }
}

[RegisterComponent]
public sealed partial class LimeMobilityVisualsComponent : Component
{
    public Vector2 BaseOffset;
    public Angle BaseRotation;
}
