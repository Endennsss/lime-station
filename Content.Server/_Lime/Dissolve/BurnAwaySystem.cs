using Content.Shared._Lime.Dissolve;
using Robust.Shared.Player;

namespace Content.Server._Lime.Dissolve;

/// <summary>Notifies observers of burning without delaying server-side body deletion.</summary>
public sealed class BurnAwaySystem : EntitySystem
{
    /// <summary>Confirms a burn-away effect to clients currently observing the body.</summary>
    public void NotifyBurn(EntityUid uid)
    {
        RaiseNetworkEvent(new BurnAwayEvent(GetNetEntity(uid)), Filter.Pvs(uid, entityManager: EntityManager));
    }
}
