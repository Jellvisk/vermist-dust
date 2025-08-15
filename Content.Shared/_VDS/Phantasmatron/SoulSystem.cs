using System.Diagnostics.CodeAnalysis;
using Content.Shared._VDS.Vessel.Components;
using Content.Shared._VDS.Vessel.Systems;
using Content.Shared.Actions;
using Content.Shared.Tag;
using Robust.Shared.Containers;

namespace Content.Shared._VDS.Phantasmatron;

public sealed partial class SoulSystem : EntitySystem
{
    [Dependency] private readonly SharedVesselSystem _vessel = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly ILogManager _logMan = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoulComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SoulComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<SoulComponent, ComponentShutdown>(OnCompRemove);

        SubscribeLocalEvent<SoulComponent, PossessEvent>(OnPossessEvent);
        _sawmill = _logMan.GetSawmill("soul-system");
    }

    #region On
    private void OnMapInit(EntityUid uid, SoulComponent comp, MapInitEvent args)
    {
    }

    private void OnComponentStartup(EntityUid uid, SoulComponent comp, ref ComponentStartup args)
    {
        // Add enter body action.
        if (!comp.PossessAction.HasValue)
            return;
        _actions.AddAction(uid, ref comp.PossessActionEntity, comp.PossessAction);

    }

    private void OnCompRemove(EntityUid uid, SoulComponent comp, ref ComponentShutdown args)
    {

        _actions.RemoveAction(uid, comp.PossessActionEntity);
    }

    private void OnPossessEvent(EntityUid uid, SoulComponent comp, PossessEvent args)
    {
        if (args.Handled)
            return;
        try
        {
            TryPossess(uid, args.Target, comp);
            args.Handled = true;
        }
        catch (Exception e)
        {
            _sawmill.Error($"{uid} is unable to possess {args.Target}. Exception: {e}");
            return;

        }
    }
    #endregion

    #region Is & Can

    #endregion

    #region Try
    public bool TryGetValidSlot(
        EntityUid uid,
        SoulComponent comp,
        [NotNullWhen(true)] out ContainerSlot? victimSlot)
    {
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
        if (!_vessel.IsEmpty(victimSoul))
        {
            victimSlot = null;
            return false;
        }

        victimSlot = victimSoul;
        return true;
    }

    private bool TryPossess(EntityUid uid, EntityUid victim, SoulComponent? comp = null, VesselComponent? vessel = null)
    {
        // TODO: ability to make vessel comp on the fly, alongside create slots
        if (!Resolve(uid, ref comp) || !Resolve(victim, ref vessel))
            return false;

        if (!TryGetValidSlot(victim, comp, out var victimSlot))
            return false;

        if (!_vessel.TryInsert(victim, uid, victimSlot, vessel))
        {
            return false;
        }
        if (!_vessel.TryMindTransfer(uid, victim))
        {
            return false;
        }

        return true;
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
