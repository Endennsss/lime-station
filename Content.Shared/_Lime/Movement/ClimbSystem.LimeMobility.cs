using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;

#pragma warning disable IDE0130
namespace Content.Shared.Climbing.Systems;

public sealed partial class ClimbSystem
{
    /// <summary>
    /// Выполняет уже оплаченный прыжок на препятствие без обычного do-after.
    /// </summary>
    public bool TryLimeJumpVault(EntityUid user, EntityUid target, ClimbableComponent? climbable = null)
    {
        if (!Resolve(target, ref climbable, false) || !CanVault(climbable, user, target, out _))
            return false;

        var ev = new AttemptClimbEvent(user, user, target);
        RaiseLocalEvent(target, ref ev);
        if (ev.Cancelled)
            return false;

        Climb(user, user, target, silent: true, comp: climbable);
        return true;
    }
}
