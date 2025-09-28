using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._VDS.MagnetStructure.Components;
using Content.Shared._VDS.MagnetStructure.Events;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Shared._VDS.MagnetStructure;

public abstract partial class SharedMagnetStructureSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logMan = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const float MaxMagnetismForce = 500f; // Maximum magnetism force at point blank range.
    private const float MinMagnetismDistance = 0.5f; // Minimum distance for impulse magnetism, to prevent division by zero.
    private const float ForceMultiplier = 80f;   // Tuning constant for magnetism

    private EntityQuery<TransformComponent> _xformQuery;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logMan.GetSawmill("magdock");
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    #region Is/Can

    /// <summary>
    /// Checks if the magnet is valid to operate as a magnet,
    /// such as: is it powered?
    /// </summary>
    public bool IsMagnetValid(
            EntityUid uid,
            MagnetStructureComponent comp)
    {
        // check if magnet is powered or not
        if (comp.Connected || !_power.IsPowered(uid))
        {
            return false;
        }


        if (Deleted(uid))
        {
            // TODO: clear target
            _sawmill.Debug($"IsMagnetValid: magnet {uid} was somehow deleted.");
            return false;
        }

        return true;
    }

    #endregion

    #region Get

    #endregion

    #region Try

    /// <summary>
    /// Tries to return a valid EntityUid to do magnet stuff to.
    /// </summary>
    public bool TryGetValidTarget(
            EntityUid self,
            MagnetStructureComponent comp,
            [NotNullWhen(true)] out EntityUid? valid
            )
    {
        _sawmill.Debug($"TryGetValidTarget: {self} is attempting to get a valid target");
        if (!_xformQuery.TryGetComponent(self, out var transform))
        {
            _sawmill.Debug($"{self} | TryGetValidTarget: magnet {self} did not have a transform component {transform}");
            valid = null;
            return false;
        }

        var look = _lookup.GetEntitiesInRange<MagneticComponent>(transform.Coordinates, comp.Range);
        var validTargets = new List<EntityUid>();

        // add targets to list
        foreach (var target in look)
        {
            if (TryGetXFormPair(transform, target, out var _))
            {
                validTargets.Add(target);
                ApplyMagneticForce(self, target);
            }
        }

        if (!TryGetClosestDistance(
            validTargets,
            transform,
            comp.Range,
            comp.ClosestDistance,
            out var closest,
            out var closestDistance))
        {
            valid = null;
            return false;
        }

        _sawmill.Debug($"{self} | TryGetValidTarget: {closest} has been spotted with a distance of {closestDistance}.");

        // save data to comp
        comp.NearestEnt = closest;
        comp.ClosestDistance = (float)closestDistance;
        valid = comp.NearestEnt;
        Dirty(self, comp);

        return true;
    }

    ///<summary>
    /// Takes in a list of EntityUid's and tries to find the closest
    /// uid to the provided <paramref name="transform"/>.
    ///</summary>
    /// <param name="validTargets">The list of uid's to iterate through.</param>
    /// <param name="transform">The transform component that will be used to measure distance from</param>
    /// <param name="range">Maximum distance to even bother measuring.</param>
    /// <param name="priorDistance">Can feed in a distance found prior, used to ignore anything farther than this param.</param>
    /// <param name="closestEnt"> Returns the closest entity.</param>
    /// <param name="closestDistance"> Returns the closest entity's distance.</param>
    public bool TryGetClosestDistance(
            List<EntityUid> validTargets,
            TransformComponent transform,
            float? range,
            float? priorDistance,
            [NotNullWhen(true)] out EntityUid? closestEnt,
            [NotNullWhen(true)] out float? closestDistance
            )
    {
        if (validTargets.Count <= 0)
        {
            closestEnt = null;
            closestDistance = null;
            return false;
        }

        // infinite detection range if not supplied in args
        range ??= float.PositiveInfinity;

        // meant to be looped through multiple times,
        // so the ability to feed the prior distance
        // back in is nice.
        priorDistance ??= float.PositiveInfinity;

        var distances = new Dictionary<EntityUid, float>();
        var closest = priorDistance;

        // calculate distance of all targets in the list
        // add them to a dictionary to be sorted later, only adding to that dictionary
        // if the target is within range, and is closer than the last scanned target
        foreach (var target in validTargets)
        {
            if (!_xformQuery.TryGetComponent(target, out var xForm))
            {
                continue;
            }

            if (transform.Coordinates.TryDistance
                    (
                     EntityManager,
                     xForm.Coordinates,
                     out var distance)
                    && distance <= closest
                    && distance < range
               )
            {
                closest = distance; // update the closest distance we've got
                distances.Add(target, distance);
                _sawmill.Debug($"TryGetClosestDistance: Potential Candidate: {target}, distance: {distance}");
            }
        }

        switch (distances.Count)
        {
            case int count when count <= 0:
                closestEnt = null;
                closestDistance = null;
                _sawmill.Debug($"TryGetClosestDistance: Nobody is close!");
                return false;
            case 1:
                closestEnt = distances.First().Key;
                closestDistance = distances.First().Value;
                _sawmill.Debug($"TryGetClosestDistance: New Closest Entity: {closestEnt}, distance: {closestDistance}");
                return false;
            default:
                var sorted = distances.OrderBy(pair => pair.Value);
                closestEnt = sorted.First().Key;
                closestDistance = sorted.First().Value;
                _sawmill.Debug($"TryGetClosestDistance: New Closest Entity: {closestEnt}, distance: {closestDistance}");
                return true;
        }
    }
    public bool TryGetXFormPair(
            EntityUid self,
            EntityUid target,
            [NotNullWhen(true)] out TransformComponent? selfXForm,
            [NotNullWhen(true)] out TransformComponent? targetXForm
            )
    {
        if (!_xformQuery.TryGetComponent(self, out var foundSelfXForm) ||
            !_xformQuery.TryGetComponent(target, out var foundTargetXForm))
        {
            selfXForm = null;
            targetXForm = null;
            return false;
        }
        // make sure they dont exist on the same grid
        if (foundSelfXForm.GridUid == foundTargetXForm.GridUid)
        {
            selfXForm = null;
            targetXForm = null;
            return false;
        }

        selfXForm = foundSelfXForm;
        targetXForm = foundTargetXForm;
        return true;
    }

    public bool TryGetXFormPair(
            TransformComponent self,
            EntityUid target,
            [NotNullWhen(true)] out TransformComponent? targetXForm
            )
    {
        if (!_xformQuery.TryGetComponent(target, out var foundTargetXForm))
        {
            targetXForm = null;
            return false;
        }
        // make sure they dont exist on the same grid
        if (self.GridUid == foundTargetXForm.GridUid)
        {
            targetXForm = null;
            return false;
        }

        targetXForm = foundTargetXForm;
        return true;
    }

    /// <summary>
    /// Takes in two entities, returns their gridUid, transform component, and physics component.
    /// </summary>
    public bool TryGetConnectionData(
            EntityUid self,
            EntityUid target,
            [NotNullWhen(true)] out (EntityUid selfGridUid, TransformComponent selfXform, PhysicsComponent selfPhysics)? selfGrid,
            [NotNullWhen(true)] out (EntityUid targetGridUid, TransformComponent targetXform, PhysicsComponent targetPhysics)? targetGrid
            )
    {
        if (!TryGetXFormPair(
            self, target,
            out var foundSelfXForm,
            out var foundTargetXForm) ||
            !foundSelfXForm.GridUid.HasValue ||
            !foundTargetXForm.GridUid.HasValue
        )
        {
            selfGrid = null;
            targetGrid = null;
            return false;
        }

        var foundSelfGridUid = foundSelfXForm.GridUid.Value;
        var foundTargetGridUid = foundTargetXForm.GridUid.Value;
        if (!TryComp<PhysicsComponent>(foundSelfGridUid, out var foundSelfPhysics) ||
            !TryComp<PhysicsComponent>(foundTargetGridUid, out var foundTargetPhysics))
        {
            selfGrid = null;
            targetGrid = null;
            return false;
        }


        selfGrid = (
            foundSelfGridUid,
            foundSelfXForm,
            foundSelfPhysics
        );
        targetGrid = (
            foundTargetGridUid,
            foundTargetXForm,
            foundTargetPhysics
        );
        return true;
    }

    /// <summary>
    /// Try to connect two grids together, sending an
    /// event to the server if true.
    /// </summary>
    /// <param name="self"> EntityUid on the first grid to
    /// attempt the weld at.</param>
    /// <param name="target"> EntityUid on the target grid to
    /// attempt the weld at.</param>
    public bool TryConnect(
        EntityUid self,
        EntityUid target,
        float connectRange = 1.5f
        )
    {
        if (!TryGetConnectionData(
            self, target,
            out var selfGrid,
            out var targetGrid)
            || !selfGrid.HasValue
            || !targetGrid.HasValue
            )
            return false;
        _sawmill.Debug($"TryConnect: Got data.");

        var (selfGridUid, selfXForm, selfPhysics) = selfGrid.Value;
        var (targetGridUid, targetXForm, targetPhysics) = targetGrid.Value;

        if (!selfXForm.Coordinates.TryDistance(
            EntityManager,
            targetXForm.Coordinates,
            out var distance)
            || distance >= connectRange
        )
            return false;

        var packedSelf = (self, selfGridUid, selfXForm, selfPhysics);
        var packedTarget = (target, targetGridUid, targetXForm, targetPhysics);

        // raise event to server-side MagnetStructureSystem
        var ev = new MagneticConnectEvent(
                packedSelf,
                packedTarget
        );
        _sawmill.Debug($"TryConnect: Raising Event...");
        RaiseLocalEvent(self, ev);
        return true;
    }


    /// <summary>
    /// Sends an event to the server to remove a joint.
    /// </summary>
    public bool TryDisconnect(
            EntityUid self
            )
    {
        if (!TryComp<MagnetStructureComponent>(self, out var comp) || comp.MagnetJoint == null)
            return false;

        // raise event to server-side MagnetStructureSystem
        var ev = new MagneticDisconnectEvent(
                self,
                comp.MagnetJoint
        );
        _sawmill.Debug($"TryDisconnect: Raising Event...");
        RaiseLocalEvent(self, ev);
        return true;
    }
    #endregion
    #region Do

    public void ApplyMagneticForce(
            EntityUid magnet, EntityUid target,
            float magnetism = 1f,
            float magnetStrength = 2f,
            float magnetRange = 20f
            )
    {
        _sawmill.Debug($"ApplyMagneticForce: Trying to apply.");
        if (!TryGetConnectionData(
            magnet, target,
            out var magnetData,
            out var targetData)
            || !magnetData.HasValue
            || !targetData.HasValue
            )
            return;

        // use magnetstructurecomp and magneticcomponent values if they exist
        if (TryComp<MagnetStructureComponent>(magnet, out var magnetComp))
        {
            magnetStrength = magnetComp.Strength;
            magnetRange = magnetComp.Range;
        }

        if (TryComp<MagneticComponent>(target, out var targetComp))
        {
            magnetism = targetComp.Magnetism;
        }
        _sawmill.Debug($"ApplyMagneticForce: Strength = {magnetStrength}, magnetism = {magnetism}");

        var (magnetGridUid, magnetXForm, magnetPhysics) = magnetData.Value;
        var (targetGridUid, targetXForm, targetPhysics) = targetData.Value;

        if (!magnetXForm.Coordinates.TryDistance
                (
                 EntityManager,
                 targetXForm.Coordinates,
                 out var distance)
           )
            return;

        // get world direction
        var magnetWorldPos = _transform.GetWorldPosition(magnetXForm);
        var targetWorldPos = _transform.GetWorldPosition(targetXForm);

        var direction = magnetWorldPos - targetWorldPos;
        var normalizedDirection = direction / distance;

        // Use inverse square law with minimum distance clamp
        var effectiveDistance = Math.Max(distance, MinMagnetismDistance);

        // F = k * (m1 * m2) / r^2, where m1 and m2 are magnetic "charges"
        var baseForce = (magnetStrength * magnetism) / (effectiveDistance * effectiveDistance);
        var forceStrength = baseForce * ForceMultiplier;

        // distance-based falloff
        var falloffFactor = 1f - (distance / magnetRange);
        forceStrength *= falloffFactor * falloffFactor; // Quadratic falloff

        forceStrength = Math.Min(forceStrength, MaxMagnetismForce);

        // Consider object velocity for damping (prevents oscillation)
        var velocityDamping = Math.Max(0.1f, 1f - (targetPhysics.LinearVelocity.Length() * 0.1f));
        forceStrength *= velocityDamping;

        var magneticForce = normalizedDirection * forceStrength;
        _sawmill.Debug($"ApplyMagneticForce: Applying force of {magneticForce}");
        _physics.ApplyLinearImpulse(targetGridUid, magneticForce / 2, body: targetPhysics);
        _physics.ApplyLinearImpulse(magnetGridUid, magneticForce / 2, body: magnetPhysics);
    }

    #endregion
}

