using System.Linq;
using Content.Goobstation.Common.Weapons.Ranged;
using Content.Goobstation.Shared.HoldUp;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.HoldUp;

public sealed partial class HoldUpSystem : EntitySystem
{
    private const float MaxRange = 6f;
    private const float HoldUpDamageMultiplier = 1.10f;
    private const float ThreatCheckInterval = 0.1f;

    [Dependency] private GunSystem _guns = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private float _threatCheckAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatModeComponent, HoldUpClickEvent>(OnHoldUpClick);
        SubscribeLocalEvent<HeldUpComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<MeleeWeaponComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<HeldUpComponent, ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<HoldUpPendingShotComponent, AmmoShotUserEvent>(OnAmmoShot);
        SubscribeLocalEvent<HoldUpBonusProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnHoldUpClick(EntityUid uid, CombatModeComponent combat, HoldUpClickEvent args)
    {
        if (!combat.IsInCombatMode || TerminatingOrDeleted(args.Target) || uid == args.Target)
            return;

        if (!_guns.TryGetGun(uid, out var heldGun))
            return;

        var weapon = heldGun.Owner;

        if (TryComp<HeldUpComponent>(args.Target, out var existing) &&
            existing.Active &&
            existing.Aggressor == uid)
        {
            EndHoldUp(args.Target, true);
            return;
        }

        BeginHoldUp(uid, args.Target, weapon);
    }

    public bool BeginHoldUp(EntityUid aggressor, EntityUid target, EntityUid weapon)
    {
        if (aggressor == target)
            return false;

        if (TryComp<HeldUpComponent>(target, out var existing) && existing.Active)
            return false;

        if (!IsThreatValid(aggressor, target, weapon))
            return false;

        var comp = EnsureComp<HeldUpComponent>(target);
        comp.Active = true;
        comp.Aggressor = aggressor;
        comp.Weapon = weapon;
        comp.LastObservedMoveButtons = TryComp<InputMoverComponent>(target, out var mover)
            ? mover.HeldMoveButtons
            : MoveButtons.None;

        _popup.PopupEntity(
            Loc.GetString("hold-up-target-warning"),
            target,
            target,
            PopupType.LargeCaution);

        _popup.PopupEntity(
            Loc.GetString("hold-up-aggressor-confirmation"),
            aggressor,
            aggressor);

        return true;
    }

    private bool IsThreatValid(EntityUid aggressor, EntityUid target, EntityUid weapon)
    {
        if (TerminatingOrDeleted(aggressor) ||
            TerminatingOrDeleted(target) ||
            TerminatingOrDeleted(weapon))
            return false;

        if (!HasComp<ActorComponent>(target) || _mobState.IsIncapacitated(aggressor))
            return false;

        if (!TryComp<CombatModeComponent>(aggressor, out var combat) || !combat.IsInCombatMode)
            return false;

        if (!_guns.TryGetGun(aggressor, out var heldGun) || heldGun.Owner != weapon)
            return false;

        return _interaction.InRangeUnobstructed(aggressor, target, MaxRange);
    }

    public void EndHoldUp(EntityUid target, bool notify = false)
    {
        if (!TryComp<HeldUpComponent>(target, out var comp) || !comp.Active)
            return;

        comp.Active = false;

        if (notify && !TerminatingOrDeleted(target))
        {
            _popup.PopupEntity(
                Loc.GetString("hold-up-ended"),
                target,
                target);
        }

        RemCompDeferred<HeldUpComponent>(target);
    }

    private void OnMoveInput(EntityUid uid, HeldUpComponent comp, ref MoveInputEvent args)
    {
        if (!comp.Active || args.Entity.Owner != uid)
            return;

        var current = args.Entity.Comp.HeldMoveButtons;
        var previous = comp.LastObservedMoveButtons;
        comp.LastObservedMoveButtons = current;

        var newlyPressed = current & ~previous;
        if ((newlyPressed & MoveButtons.AnyDirection) == MoveButtons.None)
            return;

        PunishBreakingHoldUp(uid);
    }

    private void OnMeleeAttempt(EntityUid uid, MeleeWeaponComponent weapon, ref AttemptMeleeEvent args)
    {
        var attacker = args.User;
        if (!TryComp<HeldUpComponent>(attacker, out var heldUp) || !heldUp.Active)
            return;

        PunishBreakingHoldUp(attacker);
    }

    private void OnShotAttempt(EntityUid uid, HeldUpComponent comp, ref ShotAttemptedEvent args)
    {
        if (!comp.Active || args.User != uid)
            return;

        PunishBreakingHoldUp(uid);
    }

    public void PunishBreakingHoldUp(EntityUid target)
    {
        if (!TryComp<HeldUpComponent>(target, out var comp) || !comp.Active)
            return;

        var aggressor = comp.Aggressor;
        var weapon = comp.Weapon;

        if (!IsThreatValid(aggressor, target, weapon))
        {
            EndHoldUp(target, true);
            return;
        }

        EndHoldUp(target);
        TryFireAtTarget(aggressor, target, weapon);
    }

    private bool TryFireAtTarget(EntityUid aggressor, EntityUid target, EntityUid weapon)
    {
        if (!IsThreatValid(aggressor, target, weapon))
            return false;

        if (!_guns.TryGetGun(aggressor, out var heldGun))
            return false;

        var pending = EnsureComp<HoldUpPendingShotComponent>(aggressor);
        pending.Target = target;
        pending.Weapon = weapon;
        pending.Active = true;

        var fired = _guns.AttemptShoot(
            aggressor,
            heldGun,
            Transform(target).Coordinates,
            target);

        pending.Active = false;
        RemCompDeferred<HoldUpPendingShotComponent>(aggressor);

        if (!fired)
            return false;

        _popup.PopupEntity(
            Loc.GetString("hold-up-target-shot"),
            target,
            target);

        _popup.PopupEntity(
            Loc.GetString("hold-up-aggressor-shot"),
            aggressor,
            aggressor);

        return true;
    }

    private void OnAmmoShot(EntityUid uid, HoldUpPendingShotComponent comp, AmmoShotUserEvent args)
    {
        if (!comp.Active || args.Gun != comp.Weapon)
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            if (!HasComp<ProjectileComponent>(projectile))
                continue;

            var bonus = EnsureComp<HoldUpBonusProjectileComponent>(projectile);
            bonus.Target = comp.Target;
            bonus.Consumed = false;
        }
    }

    private void OnProjectileHit(EntityUid uid, HoldUpBonusProjectileComponent comp, ref ProjectileHitEvent args)
    {
        if (comp.Consumed || args.Target != comp.Target)
            return;

        comp.Consumed = true;

        foreach (var damageType in args.Damage.DamageDict.Keys.ToArray())
        {
            var baseDamage = args.Damage.DamageDict[damageType];
            if (baseDamage <= 0)
                continue;

            args.Damage.DamageDict[damageType] = baseDamage * HoldUpDamageMultiplier;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _threatCheckAccumulator += frameTime;
        if (_threatCheckAccumulator < ThreatCheckInterval)
            return;

        _threatCheckAccumulator = 0f;
        var query = EntityQueryEnumerator<HeldUpComponent>();

        while (query.MoveNext(out var target, out var comp))
        {
            if (comp.Active && !IsThreatValid(comp.Aggressor, target, comp.Weapon))
                EndHoldUp(target, true);
        }
    }
}
