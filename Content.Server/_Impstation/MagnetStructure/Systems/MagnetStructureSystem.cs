using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Impstation.MagnetStructure.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Timing;

namespace Content.Server._Impstation.MagnetStructure;

public sealed partial class MagnetStructureSystem : EntitySystem

{
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ILogManager _logMan = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logMan.GetSawmill("magdock");
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<MagnetStructureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MagnetStructureComponent, MapInitEvent>(OnMapInit);
    }

    #region On
    private void OnStartup(EntityUid uid, MagnetStructureComponent comp, ref ComponentStartup args)
    {
        throw new NotImplementedException();
    }

    private void OnMapInit(Entity<MagnetStructureComponent> ent, ref MapInitEvent args)
    {
        // start the proximity detection timer.
        var comp = ent.Comp;
        comp.NextUpdate = _timing.CurTime + comp.UpdateCooldown;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<MagnetStructureComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!IsMagnetValid(uid, comp))
            {
                _sawmill.Debug($"invalid magnet");
                continue;
            }

            if (comp.NextUpdate > _timing.CurTime)
            {
                continue;
            }

            _sawmill.Debug($"fuck.");
            comp.NextUpdate += comp.UpdateCooldown;
            DirtyField(uid, comp, nameof(MagnetStructureComponent.NextUpdate));
            TryGetValidTarget(uid, comp, out var valid);
        }
    }
    #endregion
    #region Is

    /// <summary>
    /// Checks if the magnet is valid to operate as a magnet,
    /// such as: is it powered?
    /// </summary>
    public bool IsMagnetValid(
            EntityUid uid,
            MagnetStructureComponent comp
            )
    {
        // check if magnet is powered or not
        if (!_power.IsPowered(uid))
        {
            _sawmill.Debug($"IsMagnetValid: magnet {uid} does not have power.");
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

        var query = EntityQueryEnumerator<TagComponent>();
        var validTargets = new List<EntityUid>();

        // check if any tags match
        while (query.MoveNext(out var target, out var _))
        {
            if (_tag.HasAnyTag(target, comp.ConnectsTo))
            {
                validTargets.Add(target);
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
        Dirty(self, comp);

        valid = comp.NearestEnt;
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
                    || distance < range
                    || distance <= closest
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
                var sorted = distances.OrderByDescending(pair => pair.Value);
                closestEnt = sorted.First().Key;
                closestDistance = sorted.First().Value;
                _sawmill.Debug($"TryGetClosestDistance: New Closest Entity: {closestEnt}, distance: {closestDistance}");
                return true;
        }
    }
    #endregion
}
