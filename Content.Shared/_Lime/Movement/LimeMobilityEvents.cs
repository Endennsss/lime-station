namespace Content.Shared._Lime.Movement;

[ByRefEvent]
public record struct LimeRollAttemptEvent(float StaminaCost, bool Cancelled = false);

[ByRefEvent]
public record struct LimeJumpAttemptEvent(float StaminaCost, bool Cancelled = false);
