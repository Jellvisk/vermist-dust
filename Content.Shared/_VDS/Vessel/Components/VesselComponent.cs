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
    public EntProtoId InitialController = string.Empty;

    /// <summary>
    /// The slot where the driver lives.
    /// </summary>
    public ContainerSlot ControlSlot = default!; // probably change this to a mind container maybe??

    /// <summary>
    /// ID of the ControlSlot
    /// </summary>
    [DataField]
    public string ControlId = "vessel";

    /// <summary>
    /// Whether to transfer the controller's mind back to themselves or not.
    /// </summary>
    [DataField]
    public bool TransferMindOnExit = true;

    /// <summary>
    /// If the player has an action to exit their vessel.
    /// </summary>
    [DataField]
    public bool CanExit = true;
}
