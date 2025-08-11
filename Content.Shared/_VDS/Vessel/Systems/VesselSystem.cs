using Content.Shared._VDS.Vessel.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._VDS.Vessel.Systems;

public sealed partial class VesselSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    // TODO: create the sister system/component, Vessel. applied to soul-ful things... and will allow it to enter any Vesselbearer. (likely add a whitelist as an option for finer definition too)
    // TODO: VesselHealth component/system. (separated for modularity). healing passthrough. damage passthrough?
    // TODO: spirit leave on gib.
    // TODO: chained leave action with doafter, as is proper. should probably be a bool toggle so people can use this component for other stuff too
    // TODO: cool fx and stuff.
    // TODO: !!!! generic-ize some of this to be a vehicle-like system instead?
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VesselComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VesselComponent, EntGotInsertedIntoContainerMessage>(OnInhabitedEvent);
        SubscribeLocalEvent<VesselComponent, EntGotRemovedFromContainerMessage>(OnUninhabitedEvent);

        SubscribeLocalEvent<VesselComponent, LeaveVesselEvent>(OnLeaveVesselEvent);
    }

    #region On
    private void OnMapInit(EntityUid uid, VesselComponent comp, MapInitEvent args)
    {
        // Insert soul.
        comp.SoulSlot = _container.EnsureContainer<ContainerSlot>(uid, comp.SlotId);
        // Add leave body action.

        // Spawn Initial Entity inside vessel, if any.
        if (comp.Soul == string.Empty)
            return;
        EntityManager.SpawnInContainerOrDrop(comp.Soul, uid, comp.SlotId);
    }

    /// <summary>
    /// Triggers via action event.
    /// </summary>
    private void OnLeaveVesselEvent(EntityUid uid, VesselComponent comp, LeaveVesselEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        TryLeave(uid, comp);
    }

    private void OnInhabitedEvent(EntityUid uid, VesselComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == comp.SlotId)
            return;

        var inserted = args.Entity;
        Inhabited(uid, inserted, comp);
    }
    private void OnUninhabitedEvent(EntityUid uid, VesselComponent comp, EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID == comp.SlotId)
            return;

        var removed = args.Entity;
        Uninhabited(uid, removed);
    }

    #endregion

    #region Is & Can

    public bool IsEmpty(VesselComponent comp)
    {
        return comp.SoulSlot.ContainedEntity == null;
    }

    public bool CanLeave(VesselComponent comp)
    {
        return !IsEmpty(comp) && comp.CanExit;
    }

    public bool CanInsert(EntityUid uid, EntityUid toInsert, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        return IsEmpty(comp) && _actionBlocker.CanConsciouslyPerformAction(toInsert);
    }

    #endregion

    #region Try
    /// <summary>
    /// Attempt to insert something into the Vessel.
    /// <summary>
    private bool TryInsert(EntityUid uid, EntityUid? toInsert, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (toInsert == null || comp.SoulSlot.ContainedEntity == toInsert)
            return false;

        if (!CanInsert(uid, toInsert.Value, comp))
            return false;

        Inhabited(uid, toInsert.Value, comp);
        _container.Insert(toInsert.Value, comp.SoulSlot);
        return true;
    }

    /// <summary>
    /// Attempt to eject whatever is in the Vessel.
    /// <summary>
    private bool TryLeave(EntityUid uid, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (comp.SoulSlot.ContainedEntity == null)
            return false;

        if (!CanLeave(comp))
            return false;

        var soul = comp.SoulSlot.ContainedEntity.Value;

        Uninhabited(uid, soul);
        TryMindTransfer(uid, comp);
        _container.RemoveEntity(uid, soul);

        return true;
    }

    /// <summary>
    /// Attempt to transfer mind from vessel to soul.
    /// <summary>
    private bool TryMindTransfer(EntityUid uid, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!comp.TransferMindOnExit)
            return false;

        // ensure the vessel does in fact have a mind to transfer (and obtain it)
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return false;

        var soul = comp.SoulSlot.ContainedEntity;
        _mindSystem.TransferTo(mindId, soul, mind: mind);

        return true;
    }
    #endregion

    #region Do
    private void Inhabited(EntityUid vessel, EntityUid soul, VesselComponent? comp = null)
    {
        if (!Resolve(vessel, ref comp))
            return;

        if (_net.IsClient)
            return;
        _actions.AddAction(vessel, ref comp.LeaveVesselActionEntity, comp.LeaveVesselAction, soul);
    }

    private void Uninhabited(EntityUid vessel, EntityUid soul)
    {
        _actions.RemoveProvidedActions(vessel, soul);
    }
    #endregion
    public sealed partial class LeaveVesselEvent : InstantActionEvent;
}
