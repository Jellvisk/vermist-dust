using System.Diagnostics.CodeAnalysis;
using Content.Shared._VDS.Vessel.Components;
using Content.Shared._VDS.Vessel.Systems;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared._VDS.Phantasmatron;

public sealed partial class SoulSystem : EntitySystem
{
    [Dependency] private readonly SharedVesselSystem _vessel = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly ILogManager _logMan = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoulComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SoulComponent, ComponentShutdown>(OnCompRemove);

        SubscribeLocalEvent<SoulComponent, PossessEvent>(OnPossessEvent);
        _sawmill = _logMan.GetSawmill("soul-system");
    }

    #region On
    private void OnMapInit(EntityUid uid, SoulComponent comp, MapInitEvent args)
    {
        // Add enter body action.
        if (comp.EnterBodyAction == null)
            return;
        _actionsSystem.AddAction(uid, ref comp.EnterBodyActionEntity, comp.EnterBodyAction);
    }

    private void OnCompRemove(EntityUid uid, SoulComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.EnterBodyActionEntity);
    }

    private void OnPossessEvent(EntityUid uid, SoulComponent comp, PossessEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        TryPossess(uid, args.Target, comp);
    }
    #endregion

    #region Is & Can
    public static bool IsEmpty(ContainerSlot victimSoul)
    {
        return victimSoul.ContainedEntity == null;
    }

    #endregion

    #region Try
    public bool TryGetValidSlot(EntityUid uid,
            [NotNullWhen(true)] out ContainerSlot? victimSlot,
            SoulComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
        {
            victimSlot = null;
            return false;
        }

        // check if the victim has any inhabitable tags. or continue on if
        // no tags are on your whitelist (i can do anything)
        if (comp.CanInhabitTheseTags != null && !_tagSystem.HasAnyTag(uid, comp.CanInhabitTheseTags))
        {
            victimSlot = null;
            return false;
        }

        // make sure victim has a container. if not, check if it's valid to create one instead
        if (!_container.TryGetContainer(uid, comp.SoulId, out var victimContainer))
        {
            victimSlot = null;
            return comp.CreateSlotIfValidTag;
        }

        // make sure it's a containerslot container
        if (victimContainer is not ContainerSlot victimSoul)
        {
            victimSlot = null;
            return false;
        }

        victimSlot = victimSoul;
        return IsEmpty(victimSoul);
    }

    private bool TryPossess(EntityUid uid, EntityUid victim, SoulComponent? comp = null, VesselComponent? vessel = null)
    {
        // TODO: ability to make vessel comp on the fly, alongside create slots
        if (!Resolve(uid, ref comp) || !Resolve(victim, ref vessel))
            return false;

        if (!TryGetValidSlot(victim, out var victimSlot, comp))
            return false;

        return _vessel.TryInsert(victim, uid, vessel) & _vessel.TryMindTransfer(uid, victim);
    }
    #endregion


    // private enum TransferMethod : byte
    // {
    //     None,
    //     Normal, // target matches the whitelist, slotID, and is empty.
    //     Create, // target matches the whitelist, missing the proper slot, and user has CreateSlot enabled.
    //     Force, // target matches the soulId, is empty, and user has no whitelist
    //     ForceCreate, // target is missing the proper slot, user has no whitelist, and user has CreateSlot enabled.
    // }
    //
    // public enum TransferPermission
    // {
    //     None, // no provided tags match
    //     Normal, // one or more of the provided tags match
    //     Force, // no tags were provided at all. no limits :-)
    // }

    public sealed partial class PossessEvent : EntityTargetActionEvent;
}
