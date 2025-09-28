using Content.Shared._VDS.MagnetStructure;
using Content.Shared._VDS.MagnetStructure.Components;
using Content.Shared._VDS.MagnetStructure.Events;
using Content.Shared.Power;
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

        SubscribeLocalEvent<MagnetStructureComponent, MagneticConnectEvent>(OnMagneticConnect);
        SubscribeLocalEvent<MagnetStructureComponent, MagneticDisconnectEvent>(OnMagnetDisconnect);
        SubscribeLocalEvent<MagnetStructureComponent, PowerChangedEvent>(OnPowerChangedEvent);

        SubscribeLocalEvent<MagnetStructureComponent, ComponentShutdown>(OnShutdown);
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

            if (TryGetValidTarget(uid, comp, out var target) && target.HasValue)
            {
                _sawmill.Debug($"Update: TryGetValidTarget: Returned True. TryConnect next..");
                TryConnect(uid, target.Value);
            }
        }
    }

    // undock if the component is removed/destroyed/something
    private void OnShutdown(Entity<MagnetStructureComponent> ent, ref ComponentShutdown args)
    {
        TryDisconnect(ent);
    }

    private void OnPowerChangedEvent(Entity<MagnetStructureComponent> ent, ref PowerChangedEvent args)
    {
        // TODO: Power on noise?
        if (!args.Powered)
        {
            TryDisconnect(ent.Owner);

        }
    }

    private void OnMagneticConnect(Entity<MagnetStructureComponent> ent, ref MagneticConnectEvent args)
    {
        _sawmill.Debug($"OnMagneticConnect: MagneticConnectEvent received.");
        Connect(
            args.Source,
            args.Target,
            ent.Comp.JointStiffness,
            ent.Comp.JointDamping,
            ent.Comp.MagnetJointId
            );
    }

    private void OnMagnetDisconnect(Entity<MagnetStructureComponent> ent, ref MagneticDisconnectEvent args)
    {
        _sawmill.Debug($"OnMagneticDisconnect: MagneticDisconnectEvent received from {args.Source}, joint of {args.Joint}");
        Disconnect(args.Source, args.Joint);
    }
    #endregion

    #region Can


    #endregion

    #region Try

    #endregion

    #region Do

    /// <summary>
    /// Connects two grids together. This does not check if it should be possible or not.
    /// </summary>
    /// <param name="selfGrid">A tuple containing an EntityUid, the EntityUid of its grid,
    /// a grid transform component, and a grid physics component.</param>
    /// <param name="targetGrid"><paramref name="selfGrid"/></param>
    /// <param name="comp">The MagnetStructureComponent to store and receive joint data from.</param>
    public void Connect(
        (EntityUid self, EntityUid selfGridUid, TransformComponent selfXForm, PhysicsComponent selfPhysics) selfGrid,
        (EntityUid target, EntityUid targetGridUid, TransformComponent targetXForm, PhysicsComponent targetPhysics) targetGrid,
        float jointStiffness = 2f,
        float jointDamping = 0.7f,
        string? jointId = null

    )
    {
        // unpack for ease of use
        var (self, selfGridUid, selfXForm, selfPhysics) = selfGrid;
        var (target, targetGridUid, targetXForm, targetPhysics) = targetGrid;

        // obtain joint damping and stiffness
        SharedJointSystem.LinearStiffness(
            jointStiffness,
            jointDamping,
            selfPhysics.Mass,
            targetPhysics.Mass,
            out var stiffness,
            out var damping);

        WeldJoint joint;


        // create joint or use any existing joints instead
        if (jointId != null)
        {
            joint = _jointSystem.GetOrCreateWeldJoint(selfGridUid, targetGridUid, jointId);
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

        // save data to component if possible
        if (TryComp<MagnetStructureComponent>(self, out var comp))
        {
            comp.Connected = true;
            comp.ClosestDistance = float.NegativeInfinity;
            comp.ConnectedTo = target;
            comp.MagnetJoint = joint;
            comp.MagnetJointId = joint.ID;
            Dirty(self, comp);
        }
        else
        {
            // for the sick freaks that call this method without the magnetstructurecomponent for some reason.
            _sawmill.Debug($"""
                    Connect: Merging grids {selfGrid} and {targetGrid} via magnetism, but no MagnetStructureComponent found on uid {self}. Merging grids anyway.

                    This can cause unforseen problems, but I trust you.
                    """
                    );
        }

        _sawmill.Debug($"Connect: {self} has connected to {target}, welding {selfGridUid} and {targetGridUid} with joint {joint.ID}");
    }

    /// <summary>
    /// Removes a joint.
    /// </summary>
    public void Disconnect(EntityUid uid, WeldJoint joint)
    {
        _jointSystem.RemoveJoint(joint);

        if (TryComp<MagnetStructureComponent>(uid, out var comp))
        {
            comp.Connected = false;
            comp.ClosestDistance = float.PositiveInfinity;
            comp.ConnectedTo = null;
            comp.MagnetJoint = null;
            comp.MagnetJointId = null;
            Dirty(uid, comp);
        }
    }


    #endregion
}
