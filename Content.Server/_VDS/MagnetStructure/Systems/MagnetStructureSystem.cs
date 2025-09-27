using Content.Shared._VDS.MagnetStructure;
using Content.Shared._VDS.MagnetStructure.Components;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._VDS.MagnetStructure.Systems;

public sealed partial class MagnetStructureSystem : SharedMagnetStructureSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logMan = default!;

    [Dependency] private readonly SharedJointSystem _jointSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string MagnetJoint = "magnet";

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logMan.GetSawmill("magdock");

        SubscribeLocalEvent<MagnetStructureComponent, MapInitEvent>(OnMapInit);
    }

    #region On
    private void OnMapInit(Entity<MagnetStructureComponent> ent, ref MapInitEvent args)
    {
        // start the proximity detection timer.
        var comp = ent.Comp;
        comp.NextUpdate = _timing.CurTime + comp.UpdateCooldown;
        DirtyField(ent, comp, nameof(MagnetStructureComponent.NextUpdate));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<MagnetStructureComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!IsMagnetValid(uid, comp))
            {
                continue;
            }

            if (comp.NextUpdate > _timing.CurTime)
            {
                continue;
            }

            comp.NextUpdate += comp.UpdateCooldown;
            DirtyField(uid, comp, nameof(MagnetStructureComponent.NextUpdate));

            if (TryGetValidTarget(uid, comp, out var valid) && valid.HasValue)
            {
                _sawmill.Debug($"Update: TryGetValidTarget: Returned True. TryConnect next..");
                TryConnect(uid, comp, valid.Value);
            }
        }
    }
    #endregion
    #region Can
    // public bool CanConnect()


    #endregion
    #region Try
    public bool TryDisconnect(EntityUid self)
    {


    }
    public bool TryConnect(EntityUid self, MagnetStructureComponent comp, EntityUid target)
    {
        if (comp.Connected)
            return false;

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
            || distance >= comp.ConnectRange
        )
            return false;

        var packedSelf = (self, selfGridUid, selfXForm, selfPhysics);
        var packedTarget = (target, targetGridUid, targetXForm, targetPhysics);

        // TODO: turn into event instead?
        _sawmill.Debug($"TryConnect: Trying to connect.");
        Connect(packedSelf, packedTarget, comp);

        return true;
    }
    #endregion

    #region Do

    /// <summary>
    /// Connects two grids together.
    /// </summary>
    /// <param name="selfGrid">A tuple containing an EntityUid, the EntityUid of its grid,
    /// a grid transform component, and a grid physics component.</param>
    /// <param name="targetGrid"><paramref name="selfGrid"/></param>
    /// <param name="comp">The MagnetStructureComponent to store and receive joint data from.</param>
    private void Connect(
        (EntityUid self, EntityUid selfGridUid, TransformComponent selfXForm, PhysicsComponent selfPhysics) selfGrid,
        (EntityUid target, EntityUid targetGridUid, TransformComponent targetXForm, PhysicsComponent targetPhysics) targetGrid,
        MagnetStructureComponent comp
    )
    {
        // unpack for ease of use
        var (self, selfGridUid, selfXForm, selfPhysics) = selfGrid;
        var (target, targetGridUid, targetXForm, targetPhysics) = targetGrid;

        // obtain joint damping and stiffness
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

        // settings stuff
        joint.LocalAnchorA = selfAnchor;
        joint.LocalAnchorB = targetAnchor;
        joint.ReferenceAngle = (float)(_transform.GetWorldRotation(targetXForm) - _transform.GetWorldRotation(selfXForm));
        joint.CollideConnected = true; // todo: add to comp instead of hardcode
        joint.Stiffness = stiffness;
        joint.Damping = damping;

        // yay
        comp.MagnetJoint = joint;
        comp.MagnetJointId = joint.ID;
        comp.Connected = true;
        Dirty(self, comp);

        _sawmill.Debug($"Connect: {self} has connected to {target}, welding {selfGridUid} and {targetGridUid} with joint {joint.ID}");
        // TODO: raise events
    }
    #endregion
}
