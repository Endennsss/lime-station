using Robust.Shared.Serialization;

namespace Content.Shared._Lime.Dissolve;

/// <summary>Confirms a body was burned away rather than killed or deleted normally.</summary>
[Serializable, NetSerializable]
public sealed class BurnAwayEvent(NetEntity entity) : EntityEventArgs
{
    /// <summary>Network identity of the body being burned away.</summary>
    public readonly NetEntity Entity = entity;
}
