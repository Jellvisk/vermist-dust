using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Shared._Impstation.MagnetStructure.Components;

/// <summary>
/// Allows the entity to recieve grid merge and unmerge events.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true, true), AutoGenerateComponentPause]
public sealed partial class MagnetStructureComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Connected = false;

    /// <summary>
    /// A list of entities that we are alllowed to attach to.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> ConnectsTo;
    /// <summary>
    /// When the next proximity check will trigger.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// Next time the proximity alert will update.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(2);

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
    [DataField, AutoNetworkedField]
    public float Range = 20f;

    /// <summary>
    /// The connect range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ConnectRange = 5f;
}

