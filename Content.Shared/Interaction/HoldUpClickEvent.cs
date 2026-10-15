// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Shared.Interaction;

public sealed class HoldUpClickEvent : EntityEventArgs
{
    public EntityUid Target { get; }

    public HoldUpClickEvent(EntityUid target)
    {
        Target = target;
    }
}
