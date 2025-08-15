using System.Diagnostics.CodeAnalysis;
using Content.Shared._VDS.Vessel.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mind;
using Robust.Shared.Containers;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Shared._VDS.Vessel.Systems;

public sealed partial class SharedVesselSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly ILogManager _logMan = default!;
    // TODO: VesselHealth component/system. (separated for modularity). healing passthrough. damage passthrough?
    // TODO: spirit leave on gib.
    // TODO: chained leave action with doafter, as is proper. should probably be a bool toggle so people can use this component for other stuff too
    // TODO: cool fx and stuff.
    // TODO: !!!! generic-ize some of this to be a vehicle-like system instead?
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VesselComponent, EntGotInsertedIntoContainerMessage>(OnInhabitedEvent);
        SubscribeLocalEvent<VesselComponent, EntGotRemovedFromContainerMessage>(OnUninhabitedEvent);
        SubscribeLocalEvent<VesselComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VesselComponent, ComponentShutdown>(OnCompRemove);

        SubscribeLocalEvent<VesselComponent, LeaveVesselEvent>(OnLeaveVesselEvent);

        _sawmill = _logMan.GetSawmill("soul-system");
    }

    #region On
    /// <summary>
    /// Gives the action to the entity
    /// </summary>
    private void OnComponentStartup(EntityUid uid, VesselComponent comp, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        // Insert controller slot.
        _container.EnsureContainer<ContainerSlot>(uid, comp.ControlId);

        // Spawn Initial Entity inside vessel, if any.
        if (comp.InitialController == string.Empty)
            return;

        // Spawn the initial controller, if any.
        if (!PredictedTrySpawnInContainer(comp.InitialController, uid, comp.ControlId, out var controller))
            return;

        if (!controller.HasValue)
            return;
        _sawmill.Debug("spawned :-)");

        Inhabited(uid, controller.Value, comp);

        _sawmill.Debug("inhabited :-)");
    }

    /// <summary>
    /// Removes the action from the entity.
    /// </summary>
    private void OnCompRemove(EntityUid uid, VesselComponent comp, ref ComponentShutdown args)
    {
        if (comp.ControlSlot == null || comp.ControlSlot.ContainedEntity == null)
            return;

        Uninhabited(uid, comp.ControlSlot.ContainedEntity.Value);
    }

    /// <summary>
    /// Triggers via action event.
    /// </summary>
    private void OnLeaveVesselEvent(EntityUid uid, VesselComponent comp, LeaveVesselEvent args)
    {

        _sawmill.Debug("pressed", args.ToString());
        if (args.Handled)
            return;

        args.Handled = true;
        _sawmill.Debug("attempting to leave", args);
        TryLeave(uid, comp);
    }
    /// <summary>
    /// Triggers once when something is put in the ControllerSlot.
    /// Used to do unique effects upon entry, and giving the
    /// vessel actions.
    /// </summary>
    private void OnInhabitedEvent(EntityUid uid, VesselComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.ControlId)
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
        if (args.Container.ID != comp.ControlId)
            return;

        var removed = args.Entity;
        Uninhabited(uid, removed);
    }

    #endregion

    #region Is & Can

    public bool IsEmpty(ContainerSlot slot)
    {
        if (slot.Count == 0)
            return true;
        return false;
    }

    public bool CanLeave(ContainerSlot slot)
    {
        var uid = slot.Owner;
        if (!TryComp<VesselComponent>(uid, out var comp))
            return false;
        if (comp is null)
            return false;

        return !IsEmpty(slot) && comp.CanExit;
    }

    public bool CanInsert(ContainerSlot slot)
    {
        var uid = slot.Owner;

        return IsEmpty(slot) && _actionBlocker.CanConsciouslyPerformAction(uid);
    }

    #endregion

    #region Try
    /// <summary>
    /// Attempt to eject whatever is in the Vessel.
    /// <summary>
    public bool TryGetController(
            EntityUid uid,
            string slotId,
            [NotNullWhen(true)] out EntityUid? controller)
    {
        if (!_container.TryGetContainer(uid, slotId, out var container))
        {
            controller = null;
            return false;
        }

        if (container is not ContainerSlot slot)
        {
            controller = null;
            return false;
        }

        if (IsEmpty(slot))
        {
            controller = null;
            return false;
        }

        if (!slot.ContainedEntity.HasValue)
        {
            controller = null;
            return false;
        }

        controller = slot.ContainedEntity.Value;
        return true;
    }

    /// <summary>
    /// Attempt to insert something into the Vessel.
    /// <summary>
    public bool TryInsert(EntityUid uid, EntityUid toInsert, ContainerSlot slot, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!CanInsert(slot))
            return false;

        if (_net.IsClient)
            return false;

        Inhabited(uid, toInsert, comp);
        _container.Insert(toInsert, slot);
        return true;
    }

    /// <summary>
    /// Attempt to eject whatever is in the Vessel.
    /// <summary>
    public bool TryLeave(EntityUid uid, VesselComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        _sawmill.Debug("trying to get controller", uid.ToString());
        if (!TryGetController(uid, comp.ControlId, out var controller))
        {
            _sawmill.Error("unable to get controller: " + controller.ToString());
            return false;
        }
        _sawmill.Debug("got controller: ", controller.ToString());
        if (!CanLeave(comp.ControlSlot))
            return false;

        Uninhabited(uid, controller.Value);
        TryMindTransfer(uid, controller.Value);
        _container.RemoveEntity(uid, controller.Value);

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

        EnsureComp<ActionsComponent>(controller);
        EnsureComp<ActionsComponent>(vessel);

        _actions.AddAction(vessel, ref comp.LeaveVesselActionEntity, comp.LeaveVesselAction);
    }

    private void Uninhabited(EntityUid vessel, EntityUid controller)
    {
        _actions.RemoveProvidedActions(vessel, controller);
    }
    #endregion

    public sealed partial class LeaveVesselEvent : InstantActionEvent;
}
