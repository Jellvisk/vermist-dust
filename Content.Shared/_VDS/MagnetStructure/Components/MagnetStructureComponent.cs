using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._VDS.MagnetStructure.Components;

/// <summary>
/// Allows the entity to recieve grid merge and unmerge events.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class MagnetStructureComponent : Component
{
    public WeldJoint? MagnetJoint = null;
    public string? MagnetJointId = null;

    [DataField]
    public bool Connected = false;

    [DataField]
    public EntityUid? ConnectedTo = null;

    [DataField]
    public float JointStiffness = 1f;

    [DataField]
    public float JointDamping = 0.7f;

    /// <summary>
    /// The nearest entity since its last proximity check.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? NearestEnt = default!;

    /// <summary>
    /// The closest distance recorded.
    /// </summary>
    [DataField]
    public float ClosestDistance = float.PositiveInfinity;

    /// <summary>
    /// The range of the magnet's pull.
    /// </summary>
    [DataField]
    public float Range = 20f;

    /// <summary>
    /// Strength of the magnet's pull.
    /// </summary>
    [DataField]
    public float Strength = 2f;

    /// <summary>
    /// The connect range.
    /// </summary>
    [DataField]
    public float ConnectRange = 1.5f;

    /// <summary>
    /// When the next proximity check will trigger.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// Next time the proximity alert will update.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateCooldown = TimeSpan.FromMilliseconds(500); // half a second
}

