using Content.Shared._VDS.Species.Components;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared._VDS.Species.Systems;

public sealed partial class SoulSystem : EntitySystem
{
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

        SubscribeLocalEvent<SoulComponent, EnterBodyWithActionEvent>(CanEnterBody); // this will change
        // TODO: a lot of this will need refactoring. I want to move a lot of this code to the
        // SoulBearerSystem, alongside generic-izing the names. SoulSystem can stay the same,
        // having the ability to enter practically any entity... not to scope creep but I'd
        // love to turn SoulBearerSystem into a 'vehicle' system. Mechs but more generic.
        _sawmill = _logMan.GetSawmill("soul-system");
    }

    private void OnMapInit(EntityUid uid, SoulComponent comp, MapInitEvent args)
    {
        // Add enter body action.
        if (comp.EnterBodyAction == null)
            return;
        _actionsSystem.AddAction(uid, ref comp.EnterBodyActionEntity, comp.EnterBodyAction);
    }

    private void CanEnterBody(EntityUid uid, SoulComponent comp, EnterBodyWithActionEvent args)
    {
        var victim = args.Target;
        _container.TryGetContainer(args.Target, comp.SlotId, out var victimContainer);
        var transferMethod = DetermineTransferMethod(comp, victim, victimContainer);

        switch (transferMethod)
        {
            case TransferMethod.Normal:
                TryEnterBody(uid, victim, victimContainer!);
                break;
            case TransferMethod.Force:
                TryEnterBody(uid, victim, victimContainer!);
                break;
            case TransferMethod.Create:
                CreateSlot(uid, comp, victim);
                break;
            case TransferMethod.ForceCreate:
                CreateSlot(uid, comp, victim);
                break;
            default:
                _sawmill.Error($"No cases matched. Transfer Method: {transferMethod.ToString()} {victim}");
                break;
        }
    }

    private void TryEnterBody(EntityUid uid, EntityUid victim, BaseContainer victimContainer)
    {
        // TODO: need to refactor this to actually return a bool. and be a proper 'try'
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;
        _container.Insert(uid, victimContainer);
        _mindSystem.TransferTo(mindId, victim, mind: mind);
        if (TryComp<SoulBearerComponent>(victim, out var comp))
            comp.HasSoul = true;
    }

    private void CreateSlot(EntityUid uid, SoulComponent comp, EntityUid victim)
    {
        var victimContainer = _container.EnsureContainer<ContainerSlot>(victim, comp.SlotId);
        TryEnterBody(uid, victim, victimContainer);
    }

    private TransferMethod DetermineTransferMethod(
        SoulComponent comp,
        EntityUid victim,
        BaseContainer? victimContainer
    )
    {
        if (HasComp<MindComponent>(victim))
            return TransferMethod.None; // TODO: implement swapping. for now, return.

        var transferPermission = DetermineTransferPermission(comp, victim);
        var victimSlot = victimContainer is ContainerSlot;
        var createSlot = comp.CreateSlotIfValidTag;

        return (transferPermission, victimSlot, createSlot) switch
        {
            (TransferPermission.Force, true, _) => TransferMethod.Force,
            (TransferPermission.Normal, true, _) => TransferMethod.Normal,
            (TransferPermission.Force, false, true) => TransferMethod.ForceCreate,
            (TransferPermission.Normal, false, true) => TransferMethod.Create,
            _ => TransferMethod.None,
        };
    }

    private TransferPermission DetermineTransferPermission(SoulComponent comp, EntityUid victim)
    {
        var inhabitAnything = comp.CanInhabitTheseTags is null;
        if (inhabitAnything)
            return TransferPermission.Force;

        var validTag = _tagSystem.HasAnyTag(victim, comp.CanInhabitTheseTags!);
        return validTag == true ? TransferPermission.Normal : TransferPermission.None;
    }

    private void OnCompRemove(EntityUid uid, SoulComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.EnterBodyActionEntity);
    }

    private enum TransferMethod : byte
    {
        None,
        Normal, // target matches the whitelist, slotID, and is empty.
        Create, // target matches the whitelist, missing the proper slot, and user has CreateSlot enabled.
        Force, // target matches the slotID, is empty, and user has no whitelist
        ForceCreate, // target is missing the proper slot, user has no whitelist, and user has CreateSlot enabled.
    }

    public enum TransferPermission
    {
        None,
        Normal,
        Force,
    }

    public sealed partial class EnterBodyWithActionEvent : EntityTargetActionEvent;
}
