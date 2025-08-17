using Content.Shared.Shuttles.Components;

namespace Content.Shared._Impstation.DockingVisuals;

public sealed partial class DockingVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharedDockingVisualsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void ChangeAppearance(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _appearance.OnChangeData(uid, sprite);
    }

    private void OnAppearanceChanged(EntityUid uid, SharedDockingVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (comp.Docked == true)
        {
            _appearance.SetData(uid, DockingVisuals.
        }



    }
}
