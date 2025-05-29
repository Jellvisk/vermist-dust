using System.ComponentModel;
using Content.Shared.Actions;
using Content.Shared.Containers;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._VDS.Species;

public sealed partial class SoulSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoulComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SoulComponent, ComponentShutdown>(OnCompRemove);

        SubscribeLocalEvent<SoulComponent, LeaveBodyEvent>(OnSoulLeave);
    }

    private void OnMapInit(EntityUid uid, SoulComponent comp, MapInitEvent args)
    {
        // Insert soul.
        comp.SoulSlot = _container.EnsureContainer<ContainerSlot>(uid, comp.SlotId);
        EntityManager.SpawnInContainerOrDrop(comp.SoulEntity, uid, comp.SlotId);
        // Give the user the exit body action.
        if (comp.LeaveBodyAction != default &&
            !_protoManager.TryIndex<EntityPrototype>(comp.LeaveBodyAction, out var actionProto))
            return;

        _actionsSystem.AddAction(uid, ref comp.LeaveBodyActionEntity, comp.LeaveBodyAction);
    }

    private void OnCompRemove(EntityUid uid, SoulComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.LeaveBodyActionEntity);
    }

    /// <summary>
    /// Leave this mortal coil.
    /// </summary>
    private void OnSoulLeave(EntityUid uid, SoulComponent comp, LeaveBodyEvent args)
    {
      // Make sure the DoAfter is valid.
      if ( comp.HasSoul || args.Handled || comp.Deleted)
          return;

      //Transfer mind from body to soul, then eject.
      if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
          return;
      _mindSystem.TransferTo(mindId, comp.SoulSlot.ContainedEntity);
      _container.TryRemoveFromContainer(uid, force: true);
      comp.HasSoul = false;
    }
    public sealed partial class LeaveBodyEvent : InstantActionEvent { }
}
