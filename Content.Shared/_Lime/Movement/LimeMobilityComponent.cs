using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Lime.Movement;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class LimeMobilityComponent : Component
{
    [DataField, AutoNetworkedField] public float RollDistance = 1.75f;
    [DataField, AutoNetworkedField] public float RollDuration = 0.4f;
    [DataField, AutoNetworkedField] public float RollDeceleration = 2f;
    [DataField, AutoNetworkedField] public float RollStaminaCost = 10f;
    [DataField, AutoNetworkedField] public float RollCooldown = 1.25f;
    [DataField, AutoNetworkedField] public float JumpDistance = 1.5f;
    [DataField, AutoNetworkedField] public float JumpDuration = 0.35f;
    [DataField, AutoNetworkedField] public float JumpStaminaCost = 15f;
    [DataField, AutoNetworkedField] public float JumpCooldown = 1f;
    [DataField, AutoNetworkedField] public float CrawlSpeedModifier = 0.4f;
    [DataField, AutoNetworkedField] public float StandDuration = 2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextRoll;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextJump;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimeProneComponent : Component
{
    [AutoNetworkedField] public bool IsStandingUp;
    public DoAfterId? StandDoAfter;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class LimeActiveManeuverComponent : Component
{
    [DataField, AutoNetworkedField] public LimeManeuverType Type;
    [DataField, AutoNetworkedField] public Vector2 Direction;
    [DataField, AutoNetworkedField] public float Speed;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;
}

[Serializable, NetSerializable]
public enum LimeManeuverType : byte
{
    None,
    Roll,
    Jump,
}
