// SPDX-License-Identifier: MIT

using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;

namespace Content.Goobstation.Shared.HoldUp;


/// Stores information about a player currently being held at gunpoint.

[RegisterComponent]
public sealed partial class HeldUpComponent : Component
{
    public bool Active = true;

    public EntityUid Aggressor;

    public EntityUid Weapon;

    public MoveButtons LastObservedMoveButtons;
}
