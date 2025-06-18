using Content.Shared._VDS.Species.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._VDS.Species.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SoulBearerComponent : Component
{
    /// <summary>
    /// Prototype: Action to leave.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? LeaveBodyAction = "ActionLeaveBody";

    [DataField]
    public EntityUid? LeaveBodyActionEntity;

    // TODO: soundspecifier datafield for a sound that plays when you leave the mortal coil.
    // TODO: an "unchain" action, where you leave your body but are tethered to it, taking less soul damage etc.
    ///<summary>
    /// Prototype: What IS the soul?
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SoulEntity = string.Empty;
    // TODO: make a proper soul prototype... also ensure some form of IsSoul component gets put on it. soul prototype for phantas will also need a unique health bar, phasing, etc. see doc.
    /// <summary>
    /// The place where your soul lives.
    /// </summary>
    public ContainerSlot SoulSlot = default!; // probably change this to a mind container maybe??

    [DataField]
    public string SlotId = "soul";

    public bool HasSoul = true;
}
