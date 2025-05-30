using Content.Shared._VDS.Species.Components;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Robust.Shared.Containers;

namespace Content.Shared._VDS.Species.Systems;
public sealed partial class SoulBearerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    // TODO: create the sister system/component, Soul. applied to soul-ful things... and will allow it to enter any Soulbearer. (likely add a whitelist as an option for finer definition too)
    // TODO: SoulHealth component/system. (separated for modularity). healing passthrough. damage passthrough?
    // TODO: spirit leave on gib.
    // TODO: chained leave action with doafter, as is proper. should probably be a bool toggle so people can use this component for other stuff too
    // TODO: cool fx and stuff.
    // might have to move some of this to server in the future idk
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoulBearerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SoulBearerComponent, ComponentShutdown>(OnCompRemove);

        SubscribeLocalEvent<SoulBearerComponent, LeaveBodyEvent>(OnSoulLeave);
    }

    private void OnMapInit(EntityUid uid, SoulBearerComponent comp, MapInitEvent args)
    {
        // Insert soul.
        comp.SoulSlot = _container.EnsureContainer<ContainerSlot>(uid, comp.SlotId);
        EntityManager.SpawnInContainerOrDrop(comp.SoulEntity, uid, comp.SlotId);
        // Add leave body action.
        _actionsSystem.AddAction(uid, ref comp.LeaveBodyActionEntity, comp.LeaveBodyAction);
    }

    private void OnCompRemove(EntityUid uid, SoulBearerComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.LeaveBodyActionEntity);
    }

    /// <summary>
    /// Leave this mortal coil.
    /// </summary>
    private void OnSoulLeave(EntityUid uid, SoulBearerComponent comp, LeaveBodyEvent args)
    {
      // Make sure the DoAfter is valid.
      if ( !comp.HasSoul || args.Handled || comp.Deleted)
          return;

      //Transfer mind from body to soul, then eject.
      if (!_mindSystem.TryGetMind(args.Performer, out var mindId, out var mind))
          return;
      var soul = _container.EnsureContainer<ContainerSlot>(args.Performer, comp.SlotId);
      _mindSystem.TransferTo(mindId, soul.ContainedEntity, mind: mind);
      _container.EmptyContainer(soul, force: true);
      comp.HasSoul = false;

    }
    public sealed partial class LeaveBodyEvent : InstantActionEvent;
}
