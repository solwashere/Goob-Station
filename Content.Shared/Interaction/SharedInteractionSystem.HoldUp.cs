using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared.Interaction;

/// <summary>
/// Handles the mouse interaction for holding up players.
/// Includes temporary debug logging.
/// </summary>
public abstract partial class SharedInteractionSystem
{
    private bool HandleHoldUpClick(
        ICommonSession? session,
        EntityCoordinates coords,
        EntityUid target)
    {
        // DEBUG 1:
        // Did the mouse shortcut trigger this method?
        Log.Info("[HOLDUP DEBUG 1] Mouse shortcut detected!");

        // Validate the input.
        if (!ValidateClientInput(
                session,
                coords,
                target,
                out var user))
        {
            Log.Info("[HOLDUP DEBUG 2] Input validation FAILED!");
            return true;
        }

        // Check that we have a player.
        if (user == null)
        {
            Log.Info("[HOLDUP DEBUG 3] No player found!");
            return true;
        }

        // Check that the target exists.
        if (Deleted(target))
        {
            Log.Info("[HOLDUP DEBUG 4] Target does not exist!");
            return true;
        }

        // Don't allow targeting yourself.
        if (user.Value == target)
        {
            Log.Info("[HOLDUP DEBUG 5] Cannot target yourself!");
            return true;
        }

        // DEBUG 6:
        // All basic input checks passed.
        Log.Info(
            $"[HOLDUP DEBUG 6] Valid interaction! " +
            $"User: {user.Value}, Target: {target}");

        // Send the event to the hold-up system.
        var ev = new HoldUpClickEvent(target);

        RaiseLocalEvent(user.Value, ev);

        // DEBUG 7:
        // The event was raised locally.
        Log.Info("[HOLDUP DEBUG 7] HoldUpClickEvent raised!");

        return false;
    }
}
