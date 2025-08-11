using Content.Shared._VDS.Vessel.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._VDS.Vessel.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VesselComponent : Component
{
    /// <summary>
    /// Prototype: Action to leave.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? LeaveVesselAction = "ActionLeaveVessel";

    [DataField]
    public EntityUid? LeaveVesselActionEntity;

    ///<summary>
    /// What currently inhabits this vessel.
    /// </summary>
    [DataField]
    public EntProtoId Soul = string.Empty;

    /// <summary>
    /// The slot where the soul lives.
    /// </summary>
    public ContainerSlot SoulSlot = default!; // probably change this to a mind container maybe??

    /// <summary>
    /// ID of the slot.
    /// </summary>
    [DataField]
    public string SlotId = "soul";

    /// <summary>
    /// Whether to transfer the player's mind back to their Soul or not.
    /// </summary>
    [DataField]
    public bool TransferMindOnExit = true;

    /// <summary>
    /// If the player has an action to exit their vessel.
    /// </summary>
    [DataField]
    public bool CanExit = true;
}
