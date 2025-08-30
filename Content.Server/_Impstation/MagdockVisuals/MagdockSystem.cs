using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Impstation.DockingVisuals.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Impstation.MagdockVisuals
{
    public sealed partial class MagdockSystem : SharedDockingSystem

    {
        [Dependency] private readonly DockingSystem _dockingSystem = default!;
        [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly TagSystem _tag = default!;
        [Dependency] private readonly ILogManager _logMan = default!;
        [Dependency] private readonly TransformSystem _transform = default!;

        private EntityQuery<MapGridComponent> _gridQuery;
        private EntityQuery<PhysicsComponent> _physicsQuery;
        private EntityQuery<TransformComponent> _xformQuery;

        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = _logMan.GetSawmill("magdock");
            _gridQuery = GetEntityQuery<MapGridComponent>();
            _physicsQuery = GetEntityQuery<PhysicsComponent>();
            _xformQuery = GetEntityQuery<TransformComponent>();

            SubscribeLocalEvent<DetectGridMergeComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<DetectGridMergeComponent, MapInitEvent>(OnMapInit);
        }

        #region On
        private void OnStartup(EntityUid uid, DetectGridMergeComponent comp, ref ComponentStartup args)
        {
            // this component relies on the docking component, so ensure
            // that one always exists.
            EnsureComp<DockingComponent>(uid);
        }

        private void OnMapInit(Entity<DetectGridMergeComponent> ent, ref MapInitEvent args)
        {
            // start the proximity detection timer.
            var comp = ent.Comp;
            comp.NextUpdate = _timing.CurTime + comp.UpdateCooldown;
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<DetectGridMergeComponent>();

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
                DirtyField(uid, comp, nameof(DetectGridMergeComponent.NextUpdate));
                TryGetValidTarget(uid, comp, out var valid);
            }
        }
        #endregion
        #region Is
        public bool IsMagnetValid(
                EntityUid uid,
                DetectGridMergeComponent comp
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
        /**
         * @brief [TODO:description]
         *
         * @param self [TODO:parameter]
         * @param comp [TODO:parameter]
         * @param out [TODO:parameter]
         * @return [TODO:return]
         */
        public bool TryGetValidTarget(
                EntityUid self,
                DetectGridMergeComponent comp,
                [NotNullWhen(true)] out EntityUid? valid
                )
        {
            _sawmill.Debug($"TryGetValidTarget: {self} is attempting to get a valid target");
            if (!_xformQuery.TryGetComponent(self, out var transform))
            {
                _sawmill.Debug($"TryGetValidTarget: magnet {self} did not have a transform component {transform}");
                valid = null;
                return false;
            }

            var closestDistance = float.PositiveInfinity;
            EntityUid? closestUid = null;
            var query = EntityQueryEnumerator<TagComponent>();
            var validTargets = new List<EntityUid>();

            // check if any tags match
            while (query.MoveNext(out var target, out var targetcomp))
            {
                if (_tag.HasAnyTag(target, comp.ConnectsTo))
                {
                    validTargets.Add(target);
                }
            }

            if (validTargets.Count <= 0)
            {
                valid = null;
                return false;
            }

            TryGetClosestDistance(comp, transform, closestDistance, validTargets);
            _sawmill.Debug($"TryGetValidTarget: {target} has been spotted.");

            // if (!transform.Coordinates.TryDistance(
            //     EntityManager,
            //     xForm.Coordinates,
            //     out var distance)
            //     || distance > comp.Range
            //     || distance >= closestDistance
            //     )
            // {
            //     _sawmill.Debug($"TryGetValidTarget: {target} is too far from {self}. {distance} vs max range of {comp.Range}");
            //     valid = null;
            //     return false;
            // }

            // obtain map coordinates for docking
            var selfMapCoords = _transform.GetMapCoordinates(self, transform);
            var targetMapCoords = _transform.GetMapCoordinates(target, xForm);
            var angle = _transform.GetWorldRotation(self);
            var dir = angle.GetDir().GetOpposite();

            if (!_dockingSystem.CanDock(
                selfMapCoords,
                angle,
                targetMapCoords,
                dir.ToAngle()
                ))
            {
                _sawmill.Debug(
                $"TryGetValidTarget: {self} cannot dock with {target}. coords: {selfMapCoords}, target coords: {targetMapCoords}, angles: {angle}, {dir.ToAngle()}"
                );

                valid = null;
                return false;
            }

            closestDistance = distance;
            closestUid = target;

            if (comp.NearestEnt != closestUid && closestUid.HasValue)
            {
                _sawmill.Debug($"TryGetValidTarget: attempting to dock with {closestUid}");
                comp.NearestEnt = closestUid;
                valid = comp.NearestEnt; // temp
                DirtyField(self, comp, nameof(DetectGridMergeComponent.NearestEnt));
                EnsureComp<DockingComponent>(self, out var dockA);
                EnsureComp<DockingComponent>(closestUid.Value, out var dockB);
                _dockingSystem.Dock((self, dockA), (closestUid.Value, dockB));

                _sawmill.Debug($"docked!");
                return true;
            }
            valid = null;
            return false;
        }

        public bool TryGetClosestDistance(
                List<EntityUid> validTargets,
                TransformComponent transform,
                float? range,
                float? priorDistance,
                [NotNullWhen(true)] out EntityUid? closestEnt,
                [NotNullWhen(true)] out float? closestDistance
                )
        {
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
}
