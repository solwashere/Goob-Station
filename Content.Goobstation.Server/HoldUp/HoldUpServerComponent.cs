// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Goobstation.Server.HoldUp;

[RegisterComponent]
public sealed partial class HoldUpPendingShotComponent : Component
{
    public EntityUid Target;

    public EntityUid Weapon;

    public bool Active;
}

[RegisterComponent]
public sealed partial class HoldUpBonusProjectileComponent : Component
{
    public EntityUid Target;

    public bool Consumed;
}
