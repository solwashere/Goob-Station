// SPDX-License-Identifier: AGPL-3.0-or-later


using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    private bool HandleHoldUpClick(
        ICommonSession? session,
        EntityCoordinates coords,
        EntityUid target)
    {
        if (!ValidateClientInput(
                session,
                coords,
                target,
                out var user))
            return true;

        if (user == null || Deleted(target))
            return true;

        // Prevent players from holding themselves up.
        if (user.Value == target)
            return true;

        // Send the hold-up interaction event.
        var ev = new HoldUpClickEvent(target);
        RaiseLocalEvent(user.Value, ev);

        return true;
    }
}
