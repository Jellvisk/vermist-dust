
using Content.Server.Shuttles.Events;
using Content.Shared._Impstation.DockingVisuals;
using Content.Shared._Impstation.DockingVisuals.Components;
using Content.Shared.Instruments;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Server._Impstation.MagdockVisuals;

public sealed class MagdockVisualsSystem : EntitySystem
{
    private ISawmill _sawmill = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ILogManager _logMan = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DetectGridMergeComponent, DockEvent>(OnGridDock);
        SubscribeLocalEvent<DetectGridMergeComponent, UndockEvent>(OnGridUndock);
        _sawmill = _logMan.GetSawmill("dockingvisuals");
    }

    private void OnGridDock(EntityUid uid, DetectGridMergeComponent? comp, ref DockEvent args)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Connected = true;
        Dirty(uid, comp);
        UpdateAppearance(uid);
        _sawmill.Debug("WOW YOU DOCKED!!!!!!!!!!!!!!!");
    }
    private void OnGridUndock(EntityUid uid, DetectGridMergeComponent? comp, ref UndockEvent args)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Connected = false;
        Dirty(uid, comp);
        UpdateAppearance(uid);
        _sawmill.Debug("WOW YOU UUUUUUUUNDOCKED!!!!!!!!!!!!!!!");
    }
    private void UpdateAppearance(EntityUid uid, DetectGridMergeComponent? comp = null, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref comp, ref appearance, false))
            return;

        _appearance.SetData(
                uid,
                DockingVisualLayers.Base,
                comp.Connected ? DockingVisualState.Docked : DockingVisualState.Undocked,
                appearance);

        _appearance.SetData(
                uid,
                DockingVisualLayers.Lights,
                comp.Connected ? DockingVisualState.Docked : DockingVisualState.Undocked,
                appearance);

    }
}
