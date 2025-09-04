using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Impstation.MagnetStructure.Components;
using Content.Shared._Impstation.MagnetStructure;
using Content.Shared._Impstation.MagnetStructure.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Impstation.MagnetStructure.Systems;

public sealed partial class MagnetStructureSystem : SharedMagnetStructureSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logMan = default!;

    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedJointSystem _jointSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string MagnetJoint = "magnet";

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

        SubscribeLocalEvent<MagnetStructureComponent, MapInitEvent>(OnMapInit);
    }

    #region On
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

            if (TryGetValidTarget(uid, comp, out var valid) && valid.HasValue)
            {
                TryConnect(uid, comp, valid.Value);
            }
        }
    }
    #endregion
    #region Can
    // public bool CanConnect()


    #endregion
    #region Try
    public bool TryConnect(EntityUid self, MagnetStructureComponent comp, EntityUid target)
    {
        if (!TryGetConnectionData(
            self, target,
            out var selfGrid,
            out var targetGrid))
            return false;

        var (selfGridUid, selfXForm, selfPhysics) = selfGrid.Value;
        var (targetGridUid, targetXForm, targetPhysics) = targetGrid.Value;

        SharedJointSystem.LinearStiffness(
            comp.JointStiffness,
            comp.JointDamping,
            selfPhysics.Mass,
            targetPhysics.Mass,
            out var stiffness,
            out var damping);

        WeldJoint joint;

        // use any existing joints instead
        if (comp.MagnetJointId != null)
        {
            joint = _jointSystem.GetOrCreateWeldJoint(selfGridUid, targetGridUid, comp.MagnetJointId);
        }
        else
        {
            joint = _jointSystem.GetOrCreateWeldJoint(selfGridUid, targetGridUid, MagnetJoint + self.Id);
        }

        var selfAnchor = selfXForm.LocalPosition + selfXForm.LocalRotation.ToWorldVec() / 2f;

        var targetAnchor = targetXForm.LocalPosition + targetXForm.LocalRotation.ToWorldVec() / 2f;

        joint.LocalAnchorA = selfAnchor;
        joint.LocalAnchorB = targetAnchor;
        joint.ReferenceAngle = (float)(_transform.GetWorldRotation(targetXForm) - _transform.GetWorldRotation(selfXForm));
        joint.CollideConnected = true; // todo: add to comp instead of hardcode
        joint.Stiffness = stiffness;
        joint.Damping = damping;

        comp.MagnetJoint = joint;
        comp.MagnetJointId = joint.ID;

        return true;

    }

    #endregion
}
