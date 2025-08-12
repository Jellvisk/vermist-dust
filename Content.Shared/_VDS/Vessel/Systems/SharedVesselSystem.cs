using Content.Shared._VDS.Vessel.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._VDS.Vessel.Systems;

public sealed partial class SharedVesselSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
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
        // Insert controller slot.
        comp.ControlSlot = _container.EnsureContainer<ContainerSlot>(uid, comp.ControlId);
        // Spawn Initial Entity inside vessel, if any.
        if (comp.InitialController == string.Empty)
            return;
        EntityManager.SpawnInContainerOrDrop(comp.InitialController, uid, comp.ControlId);
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
    /// <summary>
    /// Triggers once when something is put in the ControllerSlot.
    /// Used to do unique effects upon entry, and giving the
    /// vessel actions.
    /// </summary>
    private void OnInhabitedEvent(EntityUid uid, VesselComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == comp.ControlId)
            return;

        var inserted = args.Entity;
        Inhabited(uid, inserted, comp);
    }

    /// <summary>
    /// Triggers once when something is removed from the ControllerSlot.
    /// Used to do unique effects upon ejection, and removing the
    /// vessel actions.
    /// </summary>
    private void OnUninhabitedEvent(EntityUid uid, VesselComponent comp, EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID == comp.ControlId)
            return;

        var removed = args.Entity;
        Uninhabited(uid, removed);
    }

    #endregion

    #region Is & Can

    public bool IsEmpty(VesselComponent comp)
    {
        return comp.ControlSlot.ContainedEntity == null;
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
    public bool TryInsert(EntityUid uid, EntityUid? toInsert, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (toInsert == null || comp.ControlSlot.ContainedEntity == toInsert)
            return false;

        if (!CanInsert(uid, toInsert.Value, comp))
            return false;

        Inhabited(uid, toInsert.Value, comp);
        _container.Insert(toInsert.Value, comp.ControlSlot);
        return true;
    }

    /// <summary>
    /// Attempt to eject whatever is in the Vessel.
    /// <summary>
    public bool TryLeave(EntityUid uid, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (comp.ControlSlot.ContainedEntity == null)
            return false;

        if (!CanLeave(comp))
            return false;

        var controller = comp.ControlSlot.ContainedEntity.Value;
        Uninhabited(uid, controller);
        TryMindTransfer(uid, controller);
        _container.RemoveEntity(uid, controller);

        return true;
    }

    /// <summary>
    /// Attempt to transfer mind from vessel to controller.
    /// <summary>
    public bool TryMindTransfer(EntityUid self, EntityUid target)
    {
        // ensure the self does in fact have a mind to transfer (and obtain it)
        if (!_mindSystem.TryGetMind(self, out var mindId, out var mind))
            return false;

        _mindSystem.TransferTo(mindId, target, mind: mind);
        return true;
    }
    #endregion

    #region Do
    private void Inhabited(EntityUid vessel, EntityUid controller, VesselComponent? comp = null)
    {
        if (!Resolve(vessel, ref comp))
            return;

        if (_net.IsClient)
            return;
        _actions.AddAction(vessel, ref comp.LeaveVesselActionEntity, comp.LeaveVesselAction, controller);
    }

    private void Uninhabited(EntityUid vessel, EntityUid controller)
    {
        _actions.RemoveProvidedActions(vessel, controller);
    }
    #endregion

    public sealed partial class LeaveVesselEvent : InstantActionEvent;
}
