using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Impstation.MagnetStructure.Components;

/// <summary>
/// Allows the entity to recieve grid merge and unmerge events.
/// </summary>
[NetworkedComponent, AutoGenerateComponentState]
public abstract partial class SharedMagnetStructureComponent : Component
{
    [DataField]
    public bool Connected = false;

    [DataField]
    public EntityUid? ConnectedTo = null;

    [DataField]
    public float JointStiffness = 2f;

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
    /// How far to search for valid entities.
    /// </summary>
    [DataField]
    public float Range = 20f;

    /// <summary>
    /// The connect range.
    /// </summary>
    [DataField]
    public float ConnectRange = 5f;

    /// <summary>
    /// A list of entities that we are alllowed to attach to.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> ConnectsTo;
}

