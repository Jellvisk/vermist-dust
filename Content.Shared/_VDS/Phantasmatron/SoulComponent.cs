using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._VDS.Phantasmatron;

[RegisterComponent, NetworkedComponent, Access(typeof(SoulSystem)), AutoGenerateComponentState]
public sealed partial class SoulComponent : Component
{
    /// <summary>
    /// Prototype: Action to enter.
    /// </summary>
    [DataField(required: false)]
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public EntProtoId? PossessAction = "ActionPossess";

    [AutoNetworkedField]
    [DataField]
    public EntityUid? PossessActionEntity;

    /// <summary>
    /// SlotID the soul belongs to, normally.
    /// </summary>
    [DataField]
    public string SoulId = "soul";

    /// <summary>
    /// Optional list of tags the soul is allowed to inhabit.
    /// </summary>
    [DataField(required: false)]
    public List<ProtoId<TagPrototype>>? CanInhabitTheseTags;

    /// <summary>
    /// If the entity has a valid tag but no matching SoulId, allow entry anyway by
    /// creating a new slot.
    ///
    /// If set to true yet no tags are provided, the soul can inhabit anything.
    /// </summary>
    [DataField(required: false)]
    public bool CreateSlotIfValidTag = false;

    /// <summary>
    /// If the entry target is inhabited by another soul, swap places with them. Otherwise, nothing happens.
    /// </summary>
    [DataField(required: false)]
    public bool? CanSwapWithInhabitedSoulBearers = false;

    /// <summary>
    /// Force swap with minds even if the target has no 'soul', creating one for them in the process.
    /// </summary>
    [DataField(required: false)]
    public bool? ForceSwap = false;

    /// <summary>
    /// Prototype that a forcefully swapped minded entity gets placed into.
    /// </summary>
    [DataField(required: false)]
    public EntProtoId? VictimFallback;

    /// <summary>
    /// Give the force-swapped victim the SoulComponent, whether their prototype already has it or not.
    /// </summary>
    [DataField(required: false)]
    public SoulComponent? VictimFallbackSoul;
}
