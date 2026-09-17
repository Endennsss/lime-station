namespace Content.Shared._Lime.Movement;

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

[ByRefEvent]
public record struct LimeRollAttemptEvent(float StaminaCost, bool Cancelled = false);

[ByRefEvent]
public record struct LimeJumpAttemptEvent(float StaminaCost, bool Cancelled = false);

[Serializable, NetSerializable]
public sealed partial class LimeStandDoAfterEvent : SimpleDoAfterEvent;
