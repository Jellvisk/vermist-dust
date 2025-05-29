using Content.Shared.Damage;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._VDS.Species;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SoulSystem))]
public sealed partial class SoulComponent : Component
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
    public ContainerSlot SoulSlot = default!;

    [DataField]
    public string SlotId = "soul";

    public bool HasSoul = true;

    /// <summary>
    /// TODO: How much damage it takes from its host container.
    /// </summary>
    public float? DamagePassthrough;

}
